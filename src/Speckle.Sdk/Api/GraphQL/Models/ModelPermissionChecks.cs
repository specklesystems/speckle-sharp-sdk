namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelPermissionChecks
{
  public PermissionCheckResult canUpdate { get; init; }
  public PermissionCheckResult canDelete { get; init; }
  public PermissionCheckResult canCreateVersion { get; init; }
}
