namespace Speckle.Sdk.Api.GraphQL.Enums;

public enum ProjectVisibility
{
  Private,
  Public,
  [Obsolete("Use Public instead")]
  Unlisted,
  Workspace,
}
