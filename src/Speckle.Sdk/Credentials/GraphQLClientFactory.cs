using System.Net.WebSockets;
using System.Reflection;
using GraphQL.Client.Http;
using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Serialization;
using Speckle.Sdk.Api.GraphQL.Serializer;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Credentials;

[GenerateAutoInterface]
public class GraphQLClientFactory(
  ISpeckleApplication application,
  ISpeckleHttp speckleHttp,
  ILogger<GraphQLClientFactory> logger
) : IGraphQLClientFactory
{
  /// <summary>
  /// <inheritdoc cref="CreateGraphQLClient(Uri, string)"/>
  /// </summary>
  /// <param name="account">The account to use for authentication</param>
  /// <returns></returns>
  public GraphQLHttpClient CreateGraphQLClient(Account account)
  {
    return CreateGraphQLClient(new(account.serverInfo.url), account.token);
  }

  /// <summary>
  /// Creates a <see cref="GraphQLHttpClient"/> configured for communication with a Speckle server
  /// </summary>
  /// <param name="serverUrl">The base url of the speckle server to communicate with</param>
  /// <param name="authToken">If provided, all requests will be authenticated</param>
  /// <returns></returns>
  public GraphQLHttpClient CreateGraphQLClient(Uri serverUrl, string? authToken)
  {
    var gQLClient = new GraphQLHttpClient(
      new GraphQLHttpClientOptions
      {
        EndPoint = new Uri(serverUrl, "/graphql"),
        UseWebSocketForQueriesAndMutations = false,
        WebSocketProtocol = "graphql-ws",
        ConfigureWebSocketConnectionInitPayload = _ =>
        {
          return SpeckleHttp.CanAddAuth(authToken, out string? authValue) ? new { Authorization = authValue } : null;
        },
      },
      new NewtonsoftJsonSerializer(
        new JsonSerializerSettings()
        {
          ContractResolver = new CamelCasePropertyNamesContractResolver { IgnoreIsSpecifiedMembers = true }, //(Default)
          MissingMemberHandling = MissingMemberHandling.Error, //(not default) If you query for a member that doesn't exist, this will throw (except websocket responses see https://github.com/graphql-dotnet/graphql-client/issues/660)
          NullValueHandling = NullValueHandling.Ignore, //(not default) We won't serialize nulls, as can open more opportunity for conflicting with servers that are old and don't have the latest schema
          Converters = { new ConstantCaseEnumConverter() }, //(Default) enums will be serialized using the GraphQL const case standard
        }
      ),
      CreateHttpClient(authToken)
    );

    gQLClient.WebSocketReceiveErrors.Subscribe(ex =>
    {
      if (ex is WebSocketException we)
      {
        logger.LogError(
          we,
          "GraphQL Websocket received an {WebSocketErrorCode} ({NativeErrorCode}) error that has been swallowed",
          we.WebSocketErrorCode,
          we.ErrorCode
        );
      }
      else
      {
        logger.LogError(ex, "GraphQL Websocket received an error that has been swallowed");
      }
    });
    return gQLClient;
  }

  private HttpClient CreateHttpClient(string? token)
  {
    var httpClient = speckleHttp.CreateHttpClient(timeoutSeconds: 30, authorizationToken: token);

    httpClient.DefaultRequestHeaders.Add("apollographql-client-name", application.ApplicationAndVersion);
    httpClient.DefaultRequestHeaders.Add(
      "apollographql-client-version",
      Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    );
    return httpClient;
  }
}
