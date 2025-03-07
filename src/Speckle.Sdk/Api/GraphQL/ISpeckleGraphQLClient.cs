using GraphQL;
using GraphQL.Client.Http;
using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL;

internal interface ISpeckleGraphQLClient
{
  /// <exception cref="AggregateException">Request failed on the GraphQL layer, each GraphQL error will be a <see cref="SpeckleGraphQLException"/> (or subclass of) as an inner exception</exception>
  /// <exception cref="GraphQLHttpRequestException">Request failed on the HTTP layer (non-successful response code)</exception>
  /// <exception cref="HttpRequestException">The request failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation or timeout</exception>
  /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> requested a cancel</exception>
  /// <exception cref="ObjectDisposedException">This <see cref="Client"/> already been disposed</exception>
  /// <exception cref="JsonException">The response failed to deserialize, probably because the server version is incompatible with this version of the SDK, or there is a mistake in a query (queried for a property that isn't in the C# model, or a required property was null)</exception>
  internal Task<T> ExecuteGraphQLRequest<T>(GraphQLRequest request, CancellationToken cancellationToken);

  /// <exception cref="AggregateException">Containing a <see cref="SpeckleGraphQLException"/> (or subclass of) for each graphql Error</exception>
  /// <exception cref="ObjectDisposedException">This <see cref="Client"/> already been disposed</exception>
  internal IDisposable SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback);
}
