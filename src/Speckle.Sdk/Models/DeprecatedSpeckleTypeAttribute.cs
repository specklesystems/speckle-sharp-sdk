namespace Speckle.Sdk.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DeprecatedSpeckleTypeAttribute(string speckleTypeName) : Attribute
{
  public string SpeckleTypeName => speckleTypeName;
}
