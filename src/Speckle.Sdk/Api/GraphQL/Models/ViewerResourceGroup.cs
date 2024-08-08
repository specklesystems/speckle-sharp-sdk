#nullable disable

namespace Speckle.Sdk.Api.GraphQL.Models;

public class ViewerResourceGroup
{
  public string identifier { get; init; }
  public List<ViewerResourceItem> items { get; init; }
}
