namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelsTreeItem
{
  public required List<ModelsTreeItem> children { get; init; }
  public required string fullName { get; init; }
  public required bool hasChildren { get; init; }
  public required string id { get; init; }
  public required Model? model { get; init; }
  public required string name { get; init; }
  public required DateTime updatedAt { get; init; }
}
