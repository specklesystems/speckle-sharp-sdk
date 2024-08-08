#nullable disable
namespace Speckle.Sdk.Models;

[AttributeUsage(AttributeTargets.Class)]
public class SpeckleTypeAttribute(string speckleTypeName) : Attribute
{
  public string Name => speckleTypeName;
}
