namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ProjectPermissionChecks
{
  public PermissionCheckResult canCreateModel { get; init; }
  public PermissionCheckResult canDelete { get; init; }
  public PermissionCheckResult canLoad { get; init; }

  [Obsolete("Use ModelPermissionChecks.CanCreateVersion instead", true)]
  public PermissionCheckResult canPublish { get; init; }
}
