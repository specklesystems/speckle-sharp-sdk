using System.Text.RegularExpressions;
using GraphQL;
using Speckle.Sdk.Api.GraphQL.Models.Responses;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Api;

public partial class Client
{
  /// <summary>
  /// Gets the version of the current server. Useful for guarding against unsupported api calls on newer or older servers.
  /// </summary>
  /// <param name="cancellationToken">[Optional] defaults to an empty cancellation token</param>
  /// <returns><see cref="Client.Version"/> object excluding any strings (eg "2.7.2-alpha.6995" becomes "2.7.2.6995")</returns>
  /// <exception cref="SpeckleException"></exception>
  [Obsolete("Use GraphQLHttpClient.GetServerVersion instead")]
  public async Task<System.Version> GetServerVersion(CancellationToken cancellationToken = default)
  {
    var request = new GraphQLRequest
    {
      Query =
        @"query Server {
                      serverInfo {
                          version
                        }
                    }"
    };

    var res = await ExecuteGraphQLRequest<ServerInfoResponse>(request, cancellationToken).ConfigureAwait(false);

    if (res.serverInfo.version.NotNull().Contains("dev"))
    {
      return new System.Version(999, 999, 999);
    }

    var serverVersion = new System.Version(Regex.Replace(res.serverInfo.version, "[-a-zA-Z]+", ""));
    return serverVersion;
  }
}
