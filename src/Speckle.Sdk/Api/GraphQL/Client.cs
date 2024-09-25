using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Net.WebSockets;
using System.Reflection;
using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Api.GraphQL.Serializer;
using Speckle.Sdk.Credentials;
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

  internal async Task<T> ExecuteWithResiliencePolicies<T>(Func<Task<T>> func)
  {
    var delay = Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromSeconds(1), 5);
    var graphqlRetry = Policy
      .Handle<SpeckleGraphQLInternalErrorException>()
      .WaitAndRetryAsync(
        delay,
        (ex, timeout, _) =>
        {
          _logger.LogDebug(
            ex,
            "The previous attempt at executing function to get {resultType} failed with {exceptionMessage}. Retrying after {timeout}",
            typeof(T).Name,
            ex.Message,
            timeout
          );
        }
      );

    return await graphqlRetry.ExecuteAsync(func).ConfigureAwait(false);
  }

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
          MaybeThrowFromGraphQLErrors(request, result);
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

  internal void MaybeThrowFromGraphQLErrors<T>(GraphQLRequest request, GraphQLResponse<T> response)
  {
    // The errors reflect the Apollo server v2 API, which is deprecated. It is bound to change,
    // once we migrate to a newer version.
    var errors = response.Errors;
    if (errors != null && errors.Length != 0)
    {
      if (
        errors.Any(e =>
          e.Extensions != null
          && (
            e.Extensions.Contains(new KeyValuePair<string, object>("code", "FORBIDDEN"))
            || e.Extensions.Contains(new KeyValuePair<string, object>("code", "UNAUTHENTICATED"))
          )
        )
      )
      {
        throw new SpeckleGraphQLForbiddenException(request, response);
      }

      if (
        errors.Any(e =>
          e.Extensions != null && e.Extensions.Contains(new KeyValuePair<string, object>("code", "STREAM_NOT_FOUND"))
        )
      )
      {
        throw new SpeckleGraphQLStreamNotFoundException(request, response);
      }

      if (
        errors.Any(e =>
          e.Extensions != null
          && e.Extensions.Contains(new KeyValuePair<string, object>("code", "INTERNAL_SERVER_ERROR"))
        )
      )
      {
        throw new SpeckleGraphQLInternalErrorException(request, response);
      }

      throw new SpeckleGraphQLException<T>("Request failed with errors", request, response);
    }
  }

  private Dictionary<string, object?> ConvertExpandoToDict(ExpandoObject expando)
  {
    var variables = new Dictionary<string, object?>();
    foreach (KeyValuePair<string, object?> kvp in expando)
    {
      object? value;
      if (kvp.Value is ExpandoObject ex)
      {
        value = ConvertExpandoToDict(ex);
      }
      else
      {
        value = kvp.Value;
      }

      variables[kvp.Key] = value;
    }
    return variables;
  }

  /* private ILogEventEnricher[] CreateEnrichers<T>(GraphQLRequest request)
   {
     // i know this is double  (de)serializing, but we need a recursive convert to
     // dict<str, object> here
     var expando = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(request.Variables));
     var variables = request.Variables != null && expando != null ? ConvertExpandoToDict(expando) : null;
     return new ILogEventEnricher[]
     {
       new PropertyEnricher("serverUrl", ServerUrl),
       new PropertyEnricher("graphqlQuery", request.Query),
       new PropertyEnricher("graphqlVariables", variables),
       new PropertyEnricher("resultType", typeof(T).Name)
     };
   }*/

  IDisposable ISpeckleGraphQLClient.SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback) =>
    SubscribeTo(request, callback);

  /// <inheritdoc cref="ISpeckleGraphQLClient.SubscribeTo{T}"/>
  internal IDisposable SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback)
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
              MaybeThrowFromGraphQLErrors(request, response);

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
      new NewtonsoftJsonSerializer(),
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
