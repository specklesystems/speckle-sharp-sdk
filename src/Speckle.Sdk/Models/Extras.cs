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
