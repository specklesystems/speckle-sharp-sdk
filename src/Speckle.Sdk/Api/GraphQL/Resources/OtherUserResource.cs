﻿using GraphQL;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class OtherUserResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal OtherUserResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <summary>
  ///
  /// </summary>
  /// <param name="id"></param>
  /// <param name="cancellationToken"></param>
  /// <returns>the requested user, or null if the user does not exist</returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<LimitedUser?> Get(string id, CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
      query LimitedUser($id: String!) {
        data:otherUser(id: $id) {
          id
          name
          bio
          company
          avatar
          verified
          role
        }
      }
      """;

    var request = new GraphQLRequest { Query = QUERY, Variables = new { id } };

    var response = await _client
      .ExecuteGraphQLRequest<NullableResponse<LimitedUser?>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data;
  }

  /// <summary>
  /// Searches for a user on the server, by name or email
  /// </summary>
  /// <param name="query">String to search for. Must be at least 3 characters</param>
  /// <param name="limit">Max number of users to fetch</param>
  /// <param name="cursor">Optional cursor for pagination</param>
  /// <param name="archived"></param>
  /// <param name="emailOnly"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<UserSearchResultCollection> UserSearch(
    string query,
    int limit = ServerLimits.DEFAULT_PAGINATION_REQUEST,
    string? cursor = null,
    bool archived = false,
    bool emailOnly = false,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      query UserSearch($query: String!, $limit: Int!, $cursor: String, $archived: Boolean, $emailOnly: Boolean) {
        data:userSearch(query: $query, limit: $limit, cursor: $cursor, archived: $archived, emailOnly: $emailOnly) {
          cursor
          items {
           id
           name
           bio
           company
           avatar
           verified
           role
         }
       }
      }
      """;

    var request = new GraphQLRequest
    {
      Query = QUERY,
      Variables = new
      {
        query,
        limit,
        cursor,
        archived,
        emailOnly,
      },
    };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<UserSearchResultCollection>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data;
  }
}
