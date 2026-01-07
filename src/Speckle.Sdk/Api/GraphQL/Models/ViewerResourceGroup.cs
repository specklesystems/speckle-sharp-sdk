namespace Speckle.Sdk.Api.GraphQL.Models;

public class ViewerResourceGroup
{
  public required string identifier { get; init; }
  public required List<ViewerResourceItem> items { get; init; }
}
