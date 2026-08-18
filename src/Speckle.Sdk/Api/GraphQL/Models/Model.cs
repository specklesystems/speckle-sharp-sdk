namespace Speckle.Sdk.Api.GraphQL.Models;

public class Model
{
  public required LimitedUser? author { get; init; }
  public required DateTime createdAt { get; init; }
  public required string? description { get; init; }
  public required string displayName { get; init; }
  public required string id { get; init; }
  public required string name { get; init; }
  public required Uri? previewUrl { get; init; }
  public required DateTime updatedAt { get; init; }
}

public sealed class ModelWithVersions : Model
{
  public required ResourceCollection<Version> versions { get; init; }
}
