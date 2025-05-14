namespace Speckle.Sdk.Api.GraphQL.Enums;

public enum ProjectVisibility
{
  Private,

  [Obsolete("Use Unlisted instead")]
  Public,
  Unlisted,
  Workspaces,
}
