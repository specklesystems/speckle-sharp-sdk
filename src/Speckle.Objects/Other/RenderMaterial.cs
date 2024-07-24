using System.Drawing;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using Speckle.Core.Models.Instances;
using Speckle.Newtonsoft.Json;

namespace Objects.Other;

/// <summary>
/// Minimal physically based material DTO class. Based on references from
/// https://threejs.org/docs/index.html#api/en/materials/MeshStandardMaterial
/// Theoretically has equivalents in Unity and Unreal.
///
/// See: https://docs.unrealengine.com/en-US/RenderingAndGraphics/Materials/PhysicallyBased/index.html
/// And: https://blogs.unity3d.com/2014/10/29/physically-based-shading-in-unity-5-a-primer/
/// </summary>
public class RenderMaterial : Base
{
  public RenderMaterial() { }

  [SchemaInfo("RenderMaterial", "Creates a render material.", "BIM", "Other")]
  public RenderMaterial(
    double opacity = 1,
    double metalness = 0,
    double roughness = 1,
    Color? diffuse = null,
    Color? emissive = null
  )
  {
    this.opacity = opacity;
    this.metalness = metalness;
    this.roughness = roughness;
    this.diffuse = diffuse.HasValue ? diffuse.Value.ToArgb() : Color.LightGray.ToArgb();
    this.emissive = emissive.HasValue ? emissive.Value.ToArgb() : Color.Black.ToArgb();
  }

  public string name { get; set; }
  public double opacity { get; set; } = 1;
  public double metalness { get; set; }
  public double roughness { get; set; } = 1;

  [SchemaIgnore]
  public int diffuse { get; set; } = Color.LightGray.ToArgb();

  [SchemaIgnore]
  public int emissive { get; set; } = Color.Black.ToArgb();

  [JsonIgnore]
  public Color diffuseColor
  {
    get => Color.FromArgb(diffuse);
    set => diffuse = value.ToArgb();
  }

  [JsonIgnore]
  public Color emissiveColor
  {
    get => Color.FromArgb(emissive);
    set => diffuse = value.ToArgb();
  }
}

/// <summary>
/// Used to store render material to object relationships in root collections
/// </summary>
public class RenderMaterialProxy : Base, IProxyCollection
{
  public RenderMaterialProxy() { }

  public RenderMaterialProxy(RenderMaterial renderMaterial, List<string> objects)
  {
    value = renderMaterial;
    this.objects = objects;
  }

  /// <summary>
  /// The list of application ids of objects that use this render material
  /// </summary>
  public List<string> objects { get; set; }

  /// <summary>
  /// The render material used by <see cref="objects"/>
  /// </summary>
  [DetachProperty]
  public RenderMaterial value { get; set; }
}
