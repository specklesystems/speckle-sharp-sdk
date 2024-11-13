using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Net.WebSockets;
using System.Reflection;
using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Serialization;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Api.GraphQL.Serializer;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Api;

[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Justification = "Class needs refactor")]
public sealed class Client : ISpeckleGraphQLClient, IDisposable
{
  private readonly ILogger<Client> _logger;
  private readonly ISdkActivityFactory _activityFactory;
  public ProjectResource Project { get; }
  public ModelResource Model { get; }
  public VersionResource Version { get; }
  public ActiveUserResource ActiveUser { get; }
  public OtherUserResource OtherUser { get; }
  public ProjectInviteResource ProjectInvite { get; }
  public CommentResource Comment { get; }
  public SubscriptionResource Subscription { get; }

  public Uri ServerUrl => new(Account.serverInfo.url);

  [JsonIgnore]
  public Account Account { get; }

  private HttpClient HttpClient { get; }

  public GraphQLHttpClient GQLClient { get; }

  /// <param name="account"></param>
  /// <exception cref="ArgumentException"><paramref name="account"/> was null</exception>
  public Client(
    ILogger<Client> logger,
    ISdkActivityFactory activityFactory,
    ISpeckleApplication application,
    ISpeckleHttp speckleHttp,
    Account account
  )
  {
    _logger = logger;
    _activityFactory = activityFactory;
    Account = account ?? throw new ArgumentException("Provided account is null.");

    Project = new(this);
    Model = new(this);
    Version = new(this);
    ActiveUser = new(this);
    OtherUser = new(this);
    ProjectInvite = new(this);
    Comment = new(this);
    Subscription = new(this);

    HttpClient = CreateHttpClient(application, speckleHttp, account);

    GQLClient = CreateGraphQLClient(account, HttpClient);
  }

  public void Dispose()
  {
    try
    {
      Subscription.Dispose();
      GQLClient.Dispose();
    }
    catch (Exception ex) when (!ex.IsFatal()) { }
  }

  internal async Task<T> ExecuteWithResiliencePolicies<T>(Func<Task<T>> func) =>
    await GraphQLRetry
      .ExecuteAsync<T, SpeckleGraphQLInternalErrorException>(
        func,
        (
          (ex, timeout) =>
          {
            _logger.LogDebug(
              ex,
              "The previous attempt at executing function to get {resultType} failed with {exceptionMessage}. Retrying after {timeout}",
              typeof(T).Name,
              ex.Message,
              timeout
            );
          }
        )
      )
      .ConfigureAwait(false);

  /// <inheritdoc/>
  public async Task<T> ExecuteGraphQLRequest<T>(GraphQLRequest request, CancellationToken cancellationToken = default)
  {
    using var activity = _activityFactory.Start();
    activity?.SetTag("responseType", typeof(T));
    activity?.SetTag("request.query", request.Query);
    activity?.SetTag("request.operationName", request.OperationName);
    activity?.SetTag("request.variables", request.Variables);
    activity?.SetTag("request.extensions", request.Extensions);
    activity?.SetTag("clientOptions.endPoint", GQLClient.Options.EndPoint);
    activity?.SetTag("clientOptions.medaType", GQLClient.Options.MediaType);
    activity?.SetTag("clientOptions.webSocketEndPoint", GQLClient.Options.WebSocketEndPoint);
    activity?.SetTag("clientOptions.webSocketProtocol", GQLClient.Options.WebSocketProtocol);

    try
    {
      var ret = await ExecuteWithResiliencePolicies(async () =>
        {
          GraphQLResponse<T> result = await GQLClient
            .SendMutationAsync<T>(request, cancellationToken)
            .ConfigureAwait(false);
          Client.EnsureGraphQLSuccess(result.Errors);
          return result.Data;
        })
        .ConfigureAwait(false);
      activity?.SetStatus(SdkActivityStatusCode.Ok);
      return ret;
    }
    catch (Exception ex)
    {
      activity?.SetStatus(SdkActivityStatusCode.Error);
      activity?.RecordException(ex);
      throw;
    }
  }

  /// <exception cref="AggregateException"></exception>
  internal static void EnsureGraphQLSuccess(IReadOnlyList<GraphQLError>? errors)
  {
    // The errors reflect the Apollo server v2 API, which is deprecated. It is bound to change,
    // once we migrate to a newer version.
    if (errors == null || errors.Count == 0)
    {
      return;
    }

    List<SpeckleGraphQLException> exceptions = new(errors.Count);
    foreach (var error in errors)
    {
      object? code = null;
      _ = error.Extensions?.TryGetValue("code", out code);

      var message = FormatErrorMessage(error, code);
      var ex = code switch
      {
        "GRAPHQL_PARSE_FAILED" or "GRAPHQL_VALIDATION_FAILED" => new SpeckleGraphQLInvalidQueryException(message),
        "FORBIDDEN" or "UNAUTHENTICATED" => new SpeckleGraphQLForbiddenException(message),
        "STREAM_NOT_FOUND" => new SpeckleGraphQLStreamNotFoundException(message),
        "BAD_USER_INPUT" => new SpeckleGraphQLBadInputException(message),
        "INTERNAL_SERVER_ERROR" => new SpeckleGraphQLInternalErrorException(message),
        _ => new SpeckleGraphQLException(message),
      };
      exceptions.Add(ex);
    }

    throw new AggregateException("Request failed with GraphQL errors, see inner exceptions", exceptions);
  }

  [Pure]
  private static string FormatErrorMessage(GraphQLError error, object? code)
  {
    code ??= "ERROR";
    string? path = null;
    if (error.Path is not null)
    {
      path = error.Path is not null ? string.Join(',', error.Path) : null;
      path = $", at {path}";
    }
    return $"{code}: {error.Message}{path}";
  }

  IDisposable ISpeckleGraphQLClient.SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback) =>
    SubscribeTo(request, callback);

  /// <inheritdoc cref="ISpeckleGraphQLClient.SubscribeTo{T}"/>
  private IDisposable SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback)
  {
    //using (LogContext.Push(CreateEnrichers<T>(request)))
    {
      try
      {
        var res = GQLClient.CreateSubscriptionStream<T>(request);
        return res.Subscribe(
          response =>
          {
            try
            {
              EnsureGraphQLSuccess(request, response);

              if (response.Data != null)
              {
                callback(this, response.Data);
              }
              else
              {
                // Serilog.Log.ForContext("graphqlResponse", response)
                _logger.LogError(
                  "Cannot execute graphql callback for {resultType}, the response has no data.",
                  typeof(T).Name
                );
              }
            }
            // we catch forbidden to rethrow, making sure its not logged.
            catch (SpeckleGraphQLForbiddenException)
            {
              throw;
            }
            // anything else related to graphql gets logged
            catch (SpeckleGraphQLException<T> gqlException)
            {
              /* Speckle.Sdk.Logging..ForContext("graphqlResponse", gqlException.Response)
                 .ForContext("graphqlExtensions", gqlException.Extensions)
                 .ForContext("graphqlErrorMessages", gqlException.ErrorMessages.ToList())*/
              _logger.LogWarning(
                gqlException,
                "Execution of the graphql request to get {resultType} failed with {graphqlExceptionType} {exceptionMessage}.",
                typeof(T).Name,
                gqlException.GetType().Name,
                gqlException.Message
              );
              throw;
            }
            // we're not handling the bare Exception type here,
            // since we have a response object on the callback, we know the Exceptions
            // can only be thrown from the MaybeThrowFromGraphQLErrors which wraps
            // every exception into SpeckleGraphQLException
          },
          ex =>
          {
            // we're logging this as an error for now, to keep track of failures
            // so far we've swallowed these errors
            _logger.LogError(
              ex,
              "Subscription for {resultType} terminated unexpectedly with {exceptionMessage}",
              typeof(T).Name,
              ex.Message
            );
            // we could be throwing like this:
            // throw ex;
          }
        );
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        throw new SpeckleGraphQLException<T>(
          "The graphql request failed without a graphql response",
          request,
          null,
          ex
        );
      }
    }
  }

  private static GraphQLHttpClient CreateGraphQLClient(Account account, HttpClient httpClient)
  {
    var gQLClient = new GraphQLHttpClient(
      new GraphQLHttpClientOptions
      {
        EndPoint = new Uri(new Uri(account.serverInfo.url), "/graphql"),
        UseWebSocketForQueriesAndMutations = false,
        WebSocketProtocol = "graphql-ws",
        ConfigureWebSocketConnectionInitPayload = _ =>
        {
          return SpeckleHttp.CanAddAuth(account.token, out string? authValue)
            ? new { Authorization = authValue }
            : null;
        },
      },
      new NewtonsoftJsonSerializer(
        new JsonSerializerSettings()
        {
          ContractResolver = new CamelCasePropertyNamesContractResolver { IgnoreIsSpecifiedMembers = true }, //(Default)
          MissingMemberHandling = MissingMemberHandling.Error, //(not default) If you query for a member that doesn't exist, this will throw (except websocket responses see https://github.com/graphql-dotnet/graphql-client/issues/660)
          Converters =
          {
            new ConstantCaseEnumConverter(),
          } //(Default) enums will be serialized using the GraphQL const case standard
          ,
        }
      ),
      httpClient
    );

    gQLClient.WebSocketReceiveErrors.Subscribe(e =>
    {
      if (e is WebSocketException we)
      {
        Console.WriteLine(
          $"WebSocketException: {we.Message} (WebSocketError {we.WebSocketErrorCode}, ErrorCode {we.ErrorCode}, NativeErrorCode {we.NativeErrorCode}"
        );
      }
      else
      {
        Console.WriteLine($"Exception in websocket receive stream: {e}");
      }
    });
    return gQLClient;
  }

  private static HttpClient CreateHttpClient(ISpeckleApplication application, ISpeckleHttp speckleHttp, Account account)
  {
    var httpClient = speckleHttp.CreateHttpClient(timeoutSeconds: 30, authorizationToken: account.token);

    httpClient.DefaultRequestHeaders.Add("apollographql-client-name", application.ApplicationAndVersion);
    httpClient.DefaultRequestHeaders.Add(
      "apollographql-client-version",
      Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    );
    return httpClient;
  }
}
