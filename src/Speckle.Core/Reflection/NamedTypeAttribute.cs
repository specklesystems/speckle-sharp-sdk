using Speckle.Core.Common;

namespace Speckle.Core.Reflection;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NamedTypeAttribute : Attribute
{
  public string TypeName { get; private set; }
  public string TypeNameWithKeySuffix { get; private set; }

  public NamedTypeAttribute(
    Type type,
    string keySuffix)
  {
    TypeName = type.FullName.NotNull();
    
    // creating this key here needs to be consistent with where it is created
    TypeNameWithKeySuffix = CreateTypeNameWithKeySuffix(TypeName, keySuffix);
  }

  // POC: we can absolutely decide how we wish to construct this
  public static string CreateTypeNameWithKeySuffix(string typeName, string keySuffix) => $"{typeName}+{keySuffix}";
}
