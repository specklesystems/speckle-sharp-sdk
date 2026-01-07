namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class PermissionCheckResult
{
  public required bool authorized { get; init; }
  public required string code { get; init; }
  public required string message { get; init; }

  /// <exception cref="SpeckleException">Throws when <see cref="PermissionCheckResult.authorized"/> is <see langword="false"/></exception>
  public void EnsureAuthorised()
  {
    if (!authorized)
    {
      throw new WorkspacePermissionException(message);
    }
  }
}
