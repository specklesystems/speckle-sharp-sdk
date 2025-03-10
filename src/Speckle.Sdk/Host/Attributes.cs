namespace Speckle.Sdk.Host;

// TODO: this could be nuked, as it's only used to hide props on Base,
// which we might want to expose anyways...
/// <summary>
/// Used to ignore properties from expand objects etc
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SchemaIgnoreAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SchemaComputedAttribute(string name) : Attribute
{
  public string Name { get; } = name;
}
