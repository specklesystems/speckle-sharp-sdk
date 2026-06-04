using System.Drawing;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

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

  public required Color diffuse { get; set; } = Color.LightGray;

  public Color emissive { get; set; } = Color.Black;

  [JsonIgnore]
  [Obsolete($"use {nameof(diffuse)} instead")]
  public Color diffuseColor
  {
    get => diffuse;
    set => diffuse = value;
  }

  [JsonIgnore]
  [Obsolete($"use {nameof(emissive)} instead")]
  public Color emissiveColor
  {
    get => diffuse;
    set => diffuse = value;
  }
}
