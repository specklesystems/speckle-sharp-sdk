namespace Speckle.Sdk.Api.GraphQL.Enums;

public enum ProjectVisibility
{
  Private = 0,

  [Obsolete("Use Unlisted instead", true)]
  Public = 1,
  Unlisted = 2,
}
