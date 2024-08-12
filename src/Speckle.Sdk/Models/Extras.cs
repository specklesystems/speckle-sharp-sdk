#nullable disable

namespace Speckle.Sdk.Models;

/// <summary>
/// <para>In short, this helps you chunk big things into smaller things.</para>
/// See the following <see href="https://pics.me.me/chunky-boi-57848570.png">reference.</see>
/// </summary>
[SpeckleType("Speckle.Core.Models.DataChunk")]
public class DataChunk : Base
{
  public List<object> data { get; set; } = new();
}

[SpeckleType("Speckle.Core.Models.ObjectReference")]
public class ObjectReference : Base
{
  public new string speckle_type = "reference";

  public string referencedId { get; set; }

  public Dictionary<string, int> closure { get; set; }
}
