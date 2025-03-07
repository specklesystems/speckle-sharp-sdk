namespace Speckle.Sdk.Models;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SpeckleTypeAttribute(string speckleTypeName) : Attribute
{
  public string SpeckleTypeName => speckleTypeName;
}
