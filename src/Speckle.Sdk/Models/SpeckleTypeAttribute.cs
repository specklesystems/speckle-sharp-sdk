#nullable disable
namespace Speckle.Sdk.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class SpeckleTypeAttribute(string speckleTypeName) : Attribute
{
  public string Name => speckleTypeName;
}
