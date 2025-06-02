using GraphQL;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Models;

namespace Speckle.Sdk.Credentials;

public partial interface IAccountFactory
{
  internal Task<ActiveUserServerInfoResponse> GetUserServerInfo(Uri serverUrl, string authToken, CancellationToken ct);
}

[GenerateAutoInterface]
public sealed class AccountFactory(IGraphQLClientFactory graphQLClientFactory) : IAccountFactory
{
  /// <summary>
  /// Gets basic user and server information given a token and a server.
  /// </summary>
  /// <param name="serverUrl"></param>
  /// <param name="authToken"></param>
  /// <returns></returns>
  async Task<ActiveUserServerInfoResponse> IAccountFactory.GetUserServerInfo(
    Uri serverUrl,
    string authToken,
    CancellationToken ct
  ) => await GetUserServerInfoInternal(serverUrl, authToken, ct).ConfigureAwait(false);

  private async Task<ActiveUserServerInfoResponse> GetUserServerInfoInternal(
    Uri serverUrl,
    string authToken,
    CancellationToken ct
  )
  {
    using var client = graphQLClientFactory.CreateGraphQLClient(serverUrl, authToken);

    //language=graphql
    const string QUERY_STRING = """
      query {
        activeUser {
          id
          name
          email
          company
          avatar
        }
        serverInfo {
          name
          company
          adminContact
          description
          version
          migration {
            movedFrom
            movedTo
          }
        }
      }
      """;

    var request = new GraphQLRequest { Query = QUERY_STRING };

    var response = await client.SendQueryAsync<ActiveUserServerInfoResponse>(request, ct).ConfigureAwait(false);

    response.EnsureGraphQLSuccess();

    ServerInfo serverInfo = response.Data.serverInfo;
    serverInfo.url = serverUrl.ToString().TrimEnd('/');

    return response.Data;
  }

  /// <exception cref="SpeckleException">Server could not find user info given the speckleToken, suggests expired or non-existent user</exception>
  /// <inheritdoc cref="Speckle.Sdk.Api.GraphQL.GraphQLErrorHandler.EnsureGraphQLSuccess(IReadOnlyCollection{GraphQLError}?)"/>
  public async Task<Account> CreateAccount(
    Uri serverUrl,
    string speckleToken,
    string? refreshToken = null,
    CancellationToken cancellationToken = default
  )
  {
    var res = await GetUserServerInfoInternal(serverUrl, speckleToken, cancellationToken).ConfigureAwait(false);
    if (res.activeUser == null)
    {
      throw new SpeckleException("GraphQL response indicated that the ActiveUser could not be found");
    }
    return new Account()
    {
      token = speckleToken,
      refreshToken = refreshToken,
      serverInfo = res.serverInfo,
      userInfo = res.activeUser,
    };
  }
}
