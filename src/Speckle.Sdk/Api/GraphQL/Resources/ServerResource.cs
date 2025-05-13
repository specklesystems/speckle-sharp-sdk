using GraphQL;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL.Resources;

public sealed class ServerResource
{
  private readonly ISpeckleGraphQLClient _client;

  internal ServerResource(ISpeckleGraphQLClient client)
  {
    _client = client;
  }

  /// <param name="cancellationToken"></param>
  /// <returns><see langword="null"/> if server is workspaces enabled</returns>
  /// <returns>the requested user, or null if <see cref="Client"/> was initialised with an unauthenticated account</returns>
  /// <inheritdoc cref="ISpeckleGraphQLClient.ExecuteGraphQLRequest{T}"/>
  public async Task<bool> IsWorkspaceEnabled(CancellationToken cancellationToken = default)
  {
    //language=graphql
    const string QUERY = """
       query User {
        data:serverInfo {
          data:workspaces {
            workspacesEnabled      
          }
        }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY };

    var response = await _client
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<bool>>>(request, cancellationToken)
      .ConfigureAwait(false);

    return response.data.data;
  }
}
