namespace Speckle.Sdk.Api.GraphQL.Models;

public abstract class UserBase
{
  public required string? avatar { get; init; }
  public required string? bio { get; init; }
  public required string? company { get; set; }
  public required string id { get; init; }
  public required string name { get; init; }
  public required string? role { get; init; }
  public required bool? verified { get; init; }
}

public sealed class LimitedUser : UserBase
{
  public override string ToString()
  {
    return $"Other user profile: ({name} | {id})";
  }
}

public sealed class User : UserBase
{
  public required DateTime? createdAt { get; init; }
  public required string? email { get; init; }
  public required bool? hasPendingVerification { get; init; }
  public required bool? isOnboardingFinished { get; init; }
  public required List<PendingStreamCollaborator> projectInvites { get; init; }
  public required ResourceCollection<Project> projects { get; init; }

  public override string ToString()
  {
    return $"User ({email} | {name} | {id})";
  }
}
