namespace Speckle.Sdk.Api.GraphQL.Models;

public class Model
{
  public LimitedUser? author { get; init; }
  public DateTime createdAt { get; init; }
  public string? description { get; init; }
  public string displayName { get; init; }
  public string id { get; init; }
  public string name { get; init; }
  public Uri? previewUrl { get; init; }
  public DateTime updatedAt { get; init; }
}

public sealed class ModelWithVersions : Model
{
  public ResourceCollection<Version> versions { get; init; }
}
