using Speckle.Core.Models;
using Speckle.Objects.BuiltElements.Revit;

namespace Speckle.Objects.Other;

public class MappedBlockWrapper : Base
{
  public string category { get; set; } = RevitCategory.GenericModel.ToString();
  public string? nameOverride { get; set; }
  public BlockInstance instance { get; set; }

  public MappedBlockWrapper() { }

  public MappedBlockWrapper(BlockInstance instance, RevitCategory category)
  {
    this.instance = instance;
    this.category = category.ToString();
  }
}
