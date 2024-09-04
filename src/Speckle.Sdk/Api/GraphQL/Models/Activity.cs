#nullable disable
namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class Activity
{
  public string actionType { get; init; }
  public string id { get; init; }
  public Info info { get; init; }
  public string message { get; init; }
  public string resourceId { get; init; }
  public string resourceType { get; init; }
  public string streamId { get; init; }
  public DateTime time { get; init; }
  public string userId { get; init; }
}

public sealed class Info
{
  public string message { get; init; }
  public string sourceApplication { get; init; }

  public InfoCommit commit { get; init; }
}

public sealed class InfoCommit
{
  public string message { get; init; }
  public string sourceApplication { get; init; }
  public string branchName { get; init; }
}
