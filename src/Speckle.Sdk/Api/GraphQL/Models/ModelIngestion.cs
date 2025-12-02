namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelIngestion
{
  public required string id { get; init; }
  public required DateTime createdAt { get; init; }
  public required DateTime updatedAt { get; init; }
  // public required LimitedUser user { get; init; }
}
