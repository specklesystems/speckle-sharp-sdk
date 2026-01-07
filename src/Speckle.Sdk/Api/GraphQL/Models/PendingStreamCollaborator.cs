namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class PendingStreamCollaborator
{
  public required string id { get; init; }
  public required string inviteId { get; init; }
  public required string projectId { get; init; }
  public required string projectName { get; init; }
  public required string title { get; init; }
  public required string role { get; init; }
  public required LimitedUser invitedBy { get; init; }
  public required LimitedUser? user { get; init; }
  public required string? token { get; init; }
}
