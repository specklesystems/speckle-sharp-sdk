using GraphQL;
using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL;

internal interface ISpeckleGraphQLClient
{
  /// <exception cref="SpeckleGraphQLForbiddenException">"FORBIDDEN" on "UNAUTHORIZED" response from server</exception>
  /// <exception cref="SpeckleGraphQLException">All other request errors</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested a cancel</exception>
  /// <exception cref="ObjectDisposedException">This <see cref="Client"/> already been disposed</exception>
  /// <exception cref="JsonSerializationException">The response failed to deserialize, probably because the server version is incompatible with this version of the SDK, or there is a mistake in a query (queried for a property that isn't in the C# model, or a required property was null)</exception>
  internal Task<T> ExecuteGraphQLRequest<T>(GraphQLRequest request, CancellationToken cancellationToken);

  /// <exception cref="SpeckleGraphQLForbiddenException">"FORBIDDEN" on "UNAUTHORIZED" response from server</exception>
  /// <exception cref="SpeckleGraphQLException">All other request errors</exception>
  /// <exception cref="ObjectDisposedException">This <see cref="Client"/> already been disposed</exception>
  internal IDisposable SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback);
}
