using Speckle.Core.Common;

namespace Speckle.Core.Reflection;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NamedTypeAttribute : Attribute
{
  public string TypeName { get; private set; }
  public string TypeNameWithSuffix { get; private set; }

  public NamedTypeAttribute(
    Type type,
    string suffix)
  {
    TypeName = type.FullName.NotNull();
    TypeNameWithSuffix = $"TypeName{suffix}";
  }
  
  public NamedTypeAttribute(
    string typeName)
  {
    TypeName = typeName;
  }
}
