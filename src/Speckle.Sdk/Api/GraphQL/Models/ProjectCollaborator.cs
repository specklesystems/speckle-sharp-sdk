#nullable disable

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ProjectCollaborator
{
  public string role { get; init; }
  public LimitedUser user { get; init; }
}
