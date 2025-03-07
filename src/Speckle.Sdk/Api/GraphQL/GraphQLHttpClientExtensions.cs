using GraphQL;
using GraphQL.Client.Http;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Sdk.Api.GraphQL;

public static class GraphQLHttpClientExtensions
{
  /// <summary>
  /// Gets the version of the current server. Useful for guarding against unsupported api calls on newer or older servers.
  /// </summary>
  /// <remarks>
  /// Expects the response to either be<br/>
  ///  - 1. The literal string <c>dev</c>, which will return <c>999.999.999</c><br/>
  ///  - 2. A 3 numeral semver (anything after the first <c>-</c> character will be ignored)<br/>
  /// </remarks>
  /// <param name="cancellationToken"></param>
  /// <returns>A 3 numeral <see cref="Version"/> object (e.g. <c>2.21.3.alpha123</c> becomes <c>2.21.3</c>)</returns>
  /// <exception cref="AggregateException"><inheritdoc cref="GraphQLErrorHandler.EnsureGraphQLSuccess(IGraphQLResponse)"/></exception>
  /// <exception cref="FormatException">Server responded with a server version, but it was not in an expected format</exception>
  public static async Task<System.Version> GetServerVersion(
    this GraphQLHttpClient client,
    CancellationToken cancellationToken = default
  )
  {
    //lang=graphql
    const string QUERY = """
      query Server {
        data:serverInfo {
            data:version
          }
      }
      """;
    var request = new GraphQLRequest { Query = QUERY };

    var response = await client
      .SendQueryAsync<RequiredResponse<RequiredResponse<string>>>(request, cancellationToken)
      .ConfigureAwait(false);

    response.EnsureGraphQLSuccess();

    string versionString = response.Data.data.data;
    if (versionString == "dev")
    {
      return new Version(999, 999, 999);
    }

    string? semverString = versionString.Split('-').First();

    if (Version.TryParse(semverString!, out Version? semver))
    {
      return semver;
    }
    else
    {
      throw new FormatException($"Server responded with an invalid semver string  \"{semverString}\"");
    }
  }
}
