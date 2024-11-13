using GraphQL;

namespace Speckle.Sdk.Api;

/// <summary>
/// Base class for GraphQL API exceptions
/// </summary>
public class SpeckleGraphQLException<T> : SpeckleGraphQLException
{
  public new GraphQLResponse<T>? Response => (GraphQLResponse<T>?)base.Response;

  public SpeckleGraphQLException(
    string message,
    GraphQLRequest request,
    GraphQLResponse<T>? response,
    Exception? innerException = null
  )
    : base(message, request, response, innerException) { }

  public SpeckleGraphQLException() { }

  public SpeckleGraphQLException(string? message)
    : base(message) { }

  public SpeckleGraphQLException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

public class SpeckleGraphQLException : SpeckleException
{
  public SpeckleGraphQLException() { }

  public SpeckleGraphQLException(string? message)
    : base(message) { }

  public SpeckleGraphQLException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

/// <summary>
/// Represents a "FORBIDDEN" on "UNAUTHORIZED" GraphQL error as an exception.
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

public sealed class SpeckleGraphQLInternalErrorException : SpeckleGraphQLException
{
  public SpeckleGraphQLInternalErrorException() { }

  public SpeckleGraphQLInternalErrorException(string? message)
    : base(message) { }

  public SpeckleGraphQLInternalErrorException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

public sealed class SpeckleGraphQLStreamNotFoundException : SpeckleGraphQLException
{
  public SpeckleGraphQLStreamNotFoundException() { }

  public SpeckleGraphQLStreamNotFoundException(string? message)
    : base(message) { }

  public SpeckleGraphQLStreamNotFoundException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

public sealed class SpeckleGraphQLBadInputException : SpeckleGraphQLException
{
  public SpeckleGraphQLBadInputException() { }

  public SpeckleGraphQLBadInputException(string? message)
    : base(message) { }

  public SpeckleGraphQLBadInputException(string? message, Exception? innerException)
    : base(message, innerException) { }
}

public sealed class SpeckleGraphQLInvalidQueryException : SpeckleGraphQLException
{
  public SpeckleGraphQLInvalidQueryException() { }

  public SpeckleGraphQLInvalidQueryException(string? message)
    : base(message) { }

  public SpeckleGraphQLInvalidQueryException(string? message, Exception? innerException)
    : base(message, innerException) { }
}
