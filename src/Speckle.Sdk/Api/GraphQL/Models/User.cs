namespace Speckle.Sdk.Api.GraphQL.Models;

public abstract class UserBase
{
  public string? avatar { get; init; }
  public string? bio { get; init; }
  public string? company { get; set; }
  public string id { get; init; }
  public string name { get; init; }
  public string? role { get; init; }
  public bool? verified { get; init; }
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
  public DateTime? createdAt { get; init; }
  public string? email { get; init; }
  public bool? hasPendingVerification { get; init; }
  public bool? isOnboardingFinished { get; init; }
  public List<PendingStreamCollaborator> projectInvites { get; init; }
  public ResourceCollection<Project> projects { get; init; }

  public override string ToString()
  {
    return $"User ({email} | {name} | {id})";
  }
}
