namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ProjectPermissionChecks
{
  public required PermissionCheckResult canCreateModel { get; init; }
  public required PermissionCheckResult canDelete { get; init; }
  public required PermissionCheckResult canLoad { get; init; }
  public required PermissionCheckResult canPublish { get; init; }
}
