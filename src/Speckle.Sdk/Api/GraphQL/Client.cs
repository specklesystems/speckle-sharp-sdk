using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Reflection;
using GraphQL;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
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

public partial interface IClient : IDisposable
{
  GraphQLHttpClient GQLClient { get; }
}

[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Justification = "Class needs refactor")]
[GenerateAutoInterface]
public sealed class Client : ISpeckleGraphQLClient, IClient
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
  public WorkspaceResource Workspace { get; }

  public Uri ServerUrl => new(Account.serverInfo.url);

  [JsonIgnore]
  public Account Account { get; }

  private HttpClient HttpClient { get; }

  [AutoInterfaceIgnore]
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
    Workspace = new(this);

    HttpClient = CreateHttpClient(application, speckleHttp, account);

    GQLClient = CreateGraphQLClient(account, HttpClient);
  }

  [AutoInterfaceIgnore]
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
          result.EnsureGraphQLSuccess();
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

  [AutoInterfaceIgnore]
  IDisposable ISpeckleGraphQLClient.SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback) =>
    SubscribeTo(request, callback);

  /// <inheritdoc cref="ISpeckleGraphQLClient.SubscribeTo{T}"/>
  private IDisposable SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback)
  {
    try
    {
      var res = GQLClient.CreateSubscriptionStream<T>(request);
      return res.Subscribe(
        response =>
        {
          try
          {
            response.EnsureGraphQLSuccess();

            callback(this, response.Data);
          }
          catch (AggregateException ex)
          {
            _logger.LogWarning(ex, "Subscription for {type} got a response with errors", typeof(T).Name);
            throw;
          }
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
    catch (Exception ex) when (!ex.IsFatal() && ex is not ObjectDisposedException)
    {
      throw new SpeckleGraphQLException($"Subscription for {typeof(T)} failed to start", ex);
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
