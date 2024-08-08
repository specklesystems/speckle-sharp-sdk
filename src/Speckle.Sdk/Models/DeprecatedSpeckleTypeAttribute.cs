#nullable disable
namespace Speckle.Sdk.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DeprecatedSpeckleTypeAttribute(string speckleTypeName) : Attribute
{
  public string Name => speckleTypeName;
}
