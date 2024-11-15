using System.Drawing;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Objects.Other;

/// <summary>
/// Minimal physically based material DTO class. Based on references from
/// https://threejs.org/docs/index.html#api/en/materials/MeshStandardMaterial
/// Theoretically has equivalents in Unity and Unreal.
///
/// See: https://docs.unrealengine.com/en-US/RenderingAndGraphics/Materials/PhysicallyBased/index.html
/// And: https://blogs.unity3d.com/2014/10/29/physically-based-shading-in-unity-5-a-primer/
/// </summary>
[SpeckleType("Objects.Other.RenderMaterial")]
public class RenderMaterial : Base
{
  public required string name { get; set; }
  public double opacity { get; set; } = 1;
  public double metalness { get; set; }
  public double roughness { get; set; } = 1;

  public required int diffuse { get; set; } = Color.LightGray.ToArgb();

  public int emissive { get; set; } = Color.Black.ToArgb();

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public Color diffuseColor
  {
    get => Color.FromArgb(diffuse);
    set => diffuse = value.ToArgb();
  }

  [Newtonsoft.Json.JsonIgnore]
  [System.Text.Json.Serialization.JsonIgnore]
  public Color emissiveColor
  {
    get => Color.FromArgb(emissive);
    set => diffuse = value.ToArgb();
  }
}

/// <summary>
/// Used to store render material to object relationships in root collections
/// </summary>
[SpeckleType("Objects.Other.RenderMaterialProxy")]
public class RenderMaterialProxy : Base, IProxyCollection
{
  /// <summary>
  /// The list of application ids of objects that use this render material
  /// </summary>
  public required List<string> objects { get; set; }

  /// <summary>
  /// The render material used by <see cref="objects"/>
  /// </summary>
  public required RenderMaterial value { get; set; }
}
