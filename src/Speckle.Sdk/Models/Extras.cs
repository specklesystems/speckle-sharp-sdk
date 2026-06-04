namespace Speckle.Sdk.Models;

/// <summary>
/// <para>In short, this helps you chunk big things into smaller things.</para>
/// See the following <see href="https://pics.me.me/chunky-boi-57848570.png">reference.</see>
/// </summary>
[SpeckleType("Speckle.Core.Models.DataChunk")]
public sealed class DataChunk : Base
{
  public required List<object?> data { get; init; }
}

[DeprecatedSpeckleType("Speckle.Core.Models.ObjectReference")]
[SpeckleType("reference")]
public sealed class ObjectReference : Base
{
  public required string referencedId { get; init; }

  public Dictionary<string, int>? closure { get; set; }
}

/// <summary>
/// Same as <see cref="ObjectReference"/> but optimized for STJ serialisation/deserialisation
/// </summary>
internal sealed class LightWeightObjectReference : ISpeckleObject
{
  public required string referencedId { get; init; }
  public Dictionary<string, int>? closure { get; init; }
  public string? id { get; init; }
  public string? applicationId { get; init; }
  public required string speckle_type { get; init; }
}

/// <summary>
/// Same as <see cref="DataChunk"/> but optimized for STJ serialisation/deserialisation
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class LightWeightDataChunk<T> : ISpeckleObject
{
  public required List<T> data { get; init; }
  public string? id { get; init; }
  public string? applicationId { get; init; }
  public required string speckle_type { get; init; }
}
