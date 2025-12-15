namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ProjectCollaborator
{
  public required string id { get; init; }
  public required string role { get; init; }
  public required LimitedUser user { get; init; }
}
