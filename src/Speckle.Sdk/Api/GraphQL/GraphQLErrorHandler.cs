using System.Diagnostics.Contracts;
using GraphQL;

namespace Speckle.Sdk.Api.GraphQL;

internal static class GraphQLErrorHandler
{
  /// <exception cref="AggregateException"><inheritdoc cref="EnsureGraphQLSuccess(IReadOnlyCollection{GraphQLError}?)"/></exception>
  public static void EnsureGraphQLSuccess(this IGraphQLResponse response) => EnsureGraphQLSuccess(response.Errors);

  /// <exception cref="AggregateException">Containing a <see cref="SpeckleGraphQLException"/> (or subclass of) for each graphql Error</exception>
  public static void EnsureGraphQLSuccess(IReadOnlyCollection<GraphQLError>? errors)
  {
    // The errors reflect the Apollo server v2 API, which is deprecated. It is bound to change,
    // once we migrate to a newer version.
    if (errors == null || errors.Count == 0)
    {
      return;
    }

    List<SpeckleGraphQLException> exceptions = new(errors.Count);
    foreach (var error in errors)
    {
      object? code = null;
      _ = error.Extensions?.TryGetValue("code", out code);

      var message = FormatErrorMessage(error, code);
      var ex = code switch
      {
        "GRAPHQL_PARSE_FAILED" or "GRAPHQL_VALIDATION_FAILED" => new SpeckleGraphQLInvalidQueryException(message),
        "FORBIDDEN" or "UNAUTHENTICATED" => new SpeckleGraphQLForbiddenException(message),
        "STREAM_NOT_FOUND" => new SpeckleGraphQLStreamNotFoundException(message),
        "BAD_USER_INPUT" => new SpeckleGraphQLBadInputException(message),
        "INTERNAL_SERVER_ERROR" => new SpeckleGraphQLInternalErrorException(message),
        "WORKSPACES_MODULE_DISABLED_ERROR" => new SpeckleGraphQLWorkspaceNotEnabledException(message),
        _ => new SpeckleGraphQLException(message),
      };
      exceptions.Add(ex);
    }

    throw new AggregateException("Request failed with GraphQL errors, see inner exceptions", exceptions);
  }

  [Pure]
  private static string FormatErrorMessage(GraphQLError error, object? code)
  {
    code ??= "ERROR";
    return $"{code}: {error.Message}";
  }
}
