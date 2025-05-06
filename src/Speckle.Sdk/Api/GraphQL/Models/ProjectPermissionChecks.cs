namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ProjectPermissionChecks
{
  public PermissionCheckResult canCreateModel { get; init; }
  public PermissionCheckResult canDelete { get; init; }
}
