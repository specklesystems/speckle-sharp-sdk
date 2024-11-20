using Speckle.Sdk.Api.GraphQL;

namespace Speckle.Sdk.Api;

/// <summary>
/// The base class for all GraphQL errors (these are errors in the graphql response)
/// Some specific codes are maped to subtypes <see cref="GraphQLErrorHandler"/>
/// <seealso cref="SpeckleGraphQLForbiddenException"/>
/// <seealso cref="SpeckleGraphQLInternalErrorException"/>
/// <seealso cref="SpeckleGraphQLBadInputException"/>
/// <seealso cref="SpeckleGraphQLInvalidQueryException"/>
/// </summary>
public class SpeckleGraphQLException : SpeckleException
{
  public SpeckleGraphQLException() { }

  public SpeckleGraphQLException(string? message)
    : base(message) { }

  public SpeckleGraphQLException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Represents a "FORBIDDEN" or "UNAUTHORIZED" GraphQL error as an exception.
/// https://www.apollographql.com/docs/apollo-server/v2/data/errors/#unauthenticated
/// https://www.apollographql.com/docs/apollo-server/v2/data/errors/#forbidden
/// </summary>
public sealed class SpeckleGraphQLForbiddenException : SpeckleGraphQLException
{
  public SpeckleGraphQLForbiddenException() { }

  public SpeckleGraphQLForbiddenException(string? message)
    : base(message) { }

  public SpeckleGraphQLForbiddenException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Represents a "INTERNAL_SERVER_ERROR" GraphQL error as an exception.
/// https://www.apollographql.com/docs/apollo-server/v2/data/errors#internal_server_error
/// </summary>
public sealed class SpeckleGraphQLInternalErrorException : SpeckleGraphQLException
{
  public SpeckleGraphQLInternalErrorException() { }

  public SpeckleGraphQLInternalErrorException(string? message)
    : base(message) { }

  public SpeckleGraphQLInternalErrorException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Represents the custom "STREAM_NOT_FOUND" GraphQL error as an exception.
/// </summary>
public sealed class SpeckleGraphQLStreamNotFoundException : SpeckleGraphQLException
{
  public SpeckleGraphQLStreamNotFoundException() { }

  public SpeckleGraphQLStreamNotFoundException(string? message)
    : base(message) { }

  public SpeckleGraphQLStreamNotFoundException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Represents a "BAD_USER_INPUT" GraphQL error as an exception.
/// https://www.apollographql.com/docs/apollo-server/v2/data/errors#bad_user_input
/// </summary>
public sealed class SpeckleGraphQLBadInputException : SpeckleGraphQLException
{
  public SpeckleGraphQLBadInputException() { }

  public SpeckleGraphQLBadInputException(string? message)
    : base(message) { }

  public SpeckleGraphQLBadInputException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Represents a "GRAPHQL_PARSE_FAILED" or "GRAPHQL_VALIDATION_FAILED" GraphQL error as an exception.
/// https://www.apollographql.com/docs/apollo-server/v2/data/errors#graphql_parse_failed
/// https://www.apollographql.com/docs/apollo-server/v2/data/errors#graphql_validation_failed
/// </summary>
public sealed class SpeckleGraphQLInvalidQueryException : SpeckleGraphQLException
{
  public SpeckleGraphQLInvalidQueryException() { }

  public SpeckleGraphQLInvalidQueryException(string? message)
    : base(message) { }

  public SpeckleGraphQLInvalidQueryException(string? message, Exception? innerException)
    : base(message, innerException) { }
}
