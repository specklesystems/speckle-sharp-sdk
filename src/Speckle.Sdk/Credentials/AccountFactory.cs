﻿using System.Diagnostics;
using GraphQL;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Models;

namespace Speckle.Sdk.Credentials;

public partial interface IAccountFactory
{
  internal Task<ActiveUserServerInfoResponse> GetUserServerInfo(Uri serverUrl, string? authToken, CancellationToken ct);
}

[GenerateAutoInterface]
public sealed class AccountFactory(IGraphQLClientFactory graphQLClientFactory) : IAccountFactory
{
  /// <summary>
  /// Gets the User and Server info required for <see cref="Account"/> object creation
  /// </summary>
  /// <param name="serverUrl"></param>
  /// <param name="authToken">If <see lang="null"/>, the server will respond with a <see lang="null"/> <see cref="UserInfo"/></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="GetUserServerInfoInternal"/>
  [DebuggerStepThrough]
  async Task<ActiveUserServerInfoResponse> IAccountFactory.GetUserServerInfo(
    Uri serverUrl,
    string? authToken,
    CancellationToken cancellationToken
  ) => await GetUserServerInfoInternal(serverUrl, authToken, cancellationToken).ConfigureAwait(false);

  /// <exception cref="SpeckleException">Server could not find user info given the speckleToken, suggests expired or non-existent user</exception>
  /// <inheritdoc cref="Speckle.Sdk.Api.GraphQL.GraphQLErrorHandler.EnsureGraphQLSuccess(IReadOnlyCollection{GraphQLError}?)"/>
  private async Task<ActiveUserServerInfoResponse> GetUserServerInfoInternal(
    Uri serverUrl,
    string? authToken,
    CancellationToken cancellationToken
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

    var response = await client
      .SendQueryAsync<ActiveUserServerInfoResponse>(request, cancellationToken)
      .ConfigureAwait(false);

    response.EnsureGraphQLSuccess();

    ServerInfo serverInfo = response.Data.serverInfo;
    serverInfo.url = serverUrl.ToString().TrimEnd('/');

    return response.Data;
  }

  /// <summary>
  /// Creates a new <see cref="Account"/> object by fetching the required server/user information from the specified server
  /// </summary>
  /// <remarks>
  /// This does not create a new account on the server, nor does it read/write from the SQLite DB. For that see <see cref="AccountManager"/>.
  /// This is just a Factory pattern around an <see cref="Account"/> object
  /// </remarks>
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
