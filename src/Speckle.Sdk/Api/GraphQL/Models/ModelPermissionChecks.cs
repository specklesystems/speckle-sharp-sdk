namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelPermissionChecks
{
  public required PermissionCheckResult canUpdate { get; init; }
  public required PermissionCheckResult canDelete { get; init; }
  public required PermissionCheckResult canCreateVersion { get; init; }
}
