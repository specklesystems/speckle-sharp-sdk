namespace Speckle.Sdk.Models;

/// <summary>
/// Represents all different types of members that can be returned by <see cref="DynamicBase.GetMembers"/>
/// </summary>
[Flags]
public enum DynamicBaseMemberType
{
  /// <summary>
  /// The typed members of the <see cref="DynamicBase"/> object
  /// </summary>
  Instance = 1,

  /// <summary>
  /// The dynamically added members of the <see cref="DynamicBase"/> object
  /// </summary>
  Dynamic = 2,

  /// <summary>
  /// The typed members flagged with ObsoleteAttribute> attribute.
  /// </summary>
  Obsolete = 4,

  /// <summary>
  /// Old feature supported in v2 for grasshopper
  /// </summary>
  [Obsolete("Feature no longer supported")]
  SchemaComputed = 16,

  /// <summary>
  /// All the typed members, including ones with ObsoleteAttribute  attributes.
  /// </summary>
  InstanceAll = Instance + Obsolete,

  /// <summary>
  /// All the members, including dynamic and instance members flagged with ObsoleteAttribute attributes
  /// </summary>
  All = InstanceAll + Dynamic,
}
