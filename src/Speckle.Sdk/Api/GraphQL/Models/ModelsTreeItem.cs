﻿namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelsTreeItem
{
  public List<ModelsTreeItem> children { get; init; }
  public string fullName { get; init; }
  public bool hasChildren { get; init; }
  public string id { get; init; }
  public Model? model { get; init; }
  public string name { get; init; }
  public DateTime updatedAt { get; init; }
}
