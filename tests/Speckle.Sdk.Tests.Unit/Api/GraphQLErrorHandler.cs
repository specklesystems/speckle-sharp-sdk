﻿using FluentAssertions;
using GraphQL;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL;

namespace Speckle.Sdk.Tests.Unit.Api;

public class GraphQLErrorHandlerTests
{
  public static IEnumerable<object[]> ErrorCases()
  {
    yield return [typeof(SpeckleGraphQLForbiddenException), new Map { { "code", "FORBIDDEN" } }];
    yield return [typeof(SpeckleGraphQLForbiddenException), new Map { { "code", "UNAUTHENTICATED" } }];
    yield return [typeof(SpeckleGraphQLInternalErrorException), new Map { { "code", "INTERNAL_SERVER_ERROR" } }];
    yield return [typeof(SpeckleGraphQLStreamNotFoundException), new Map { { "code", "STREAM_NOT_FOUND" } }];
    yield return [typeof(SpeckleGraphQLBadInputException), new Map { { "code", "BAD_USER_INPUT" } }];
    yield return [typeof(SpeckleGraphQLInvalidQueryException), new Map { { "code", "GRAPHQL_PARSE_FAILED" } }];
    yield return [typeof(SpeckleGraphQLInvalidQueryException), new Map { { "code", "GRAPHQL_VALIDATION_FAILED" } }];
    yield return
    [
      typeof(SpeckleGraphQLWorkspaceNotEnabledException),
      new Map { { "code", "WORKSPACES_MODULE_DISABLED_ERROR" } },
    ];
    yield return [typeof(SpeckleGraphQLException), new Map { { "foo", "bar" } }];
    yield return [typeof(SpeckleGraphQLException), new Map { { "code", "CUSTOM_THING" } }];
    yield return [typeof(CannotCreateCommitException), new Map { { "code", "COMMIT_CREATE_ERROR" } }];
  }

  [Theory]
  [MemberData(nameof(ErrorCases))]
  public void TestExceptionThrowingFromGraphQLErrors(Type exType, Map extensions)
  {
    var ex = Assert.Throws<AggregateException>(() =>
      new GraphQLResponse<GraphQLClientTests.FakeGqlResponseModel>
      {
        Errors = [new() { Extensions = extensions }],
      }.EnsureGraphQLSuccess()
    );
    ex.InnerExceptions.Count.Should().Be(1);
    ex.InnerExceptions[0].Should().BeOfType(exType);
  }

  [Fact]
  public void TestMaybeThrowsDoesntThrowForNoErrors() => new GraphQLResponse<string>().EnsureGraphQLSuccess();
}
