using GraphQL;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL;

namespace Speckle.Sdk.Tests.Unit.Api;

public class GraphQLErrorHandlerTests
{
  private static IEnumerable<TestCaseData> ErrorCases()
  {
    yield return new TestCaseData(typeof(SpeckleGraphQLForbiddenException), new Map { { "code", "FORBIDDEN" } });
    yield return new TestCaseData(typeof(SpeckleGraphQLForbiddenException), new Map { { "code", "UNAUTHENTICATED" } });
    yield return new TestCaseData(
      typeof(SpeckleGraphQLInternalErrorException),
      new Map { { "code", "INTERNAL_SERVER_ERROR" } }
    );
    yield return new TestCaseData(
      typeof(SpeckleGraphQLStreamNotFoundException),
      new Map { { "code", "STREAM_NOT_FOUND" } }
    );
    yield return new TestCaseData(typeof(SpeckleGraphQLBadInputException), new Map { { "code", "BAD_USER_INPUT" } });
    yield return new TestCaseData(
      typeof(SpeckleGraphQLInvalidQueryException),
      new Map { { "code", "GRAPHQL_PARSE_FAILED" } }
    );
    yield return new TestCaseData(
      typeof(SpeckleGraphQLInvalidQueryException),
      new Map { { "code", "GRAPHQL_VALIDATION_FAILED" } }
    );
    yield return new TestCaseData(typeof(SpeckleGraphQLException), new Map { { "foo", "bar" } });
    yield return new TestCaseData(typeof(SpeckleGraphQLException), new Map { { "code", "CUSTOM_THING" } });
  }

  [Test, TestCaseSource(nameof(ErrorCases))]
  public void TestExceptionThrowingFromGraphQLErrors(Type exType, Map extensions)
  {
    var ex = Assert.Throws<AggregateException>(
      () =>
        GraphQLErrorHandler.EnsureGraphQLSuccess(
          new GraphQLResponse<GraphQLClientTests.FakeGqlResponseModel>
          {
            Errors = new GraphQLError[] { new() { Extensions = extensions } },
          }
        )
    );
    Assert.That(ex?.InnerExceptions, Has.Exactly(1).TypeOf(exType));
  }

  [Test]
  public void TestMaybeThrowsDoesntThrowForNoErrors()
  {
    Assert.DoesNotThrow(() => GraphQLErrorHandler.EnsureGraphQLSuccess(new GraphQLResponse<string>()));
  }
}
