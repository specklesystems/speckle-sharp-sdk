using Speckle.Objects;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Migration;

internal static class PipelineExtensions
{
  public static void AddDefaultSceneView(this ObjectsArtifactPipeline pipeline, IReadOnlyList<SceneViewKey> keys)
  {
    if (keys.Count > 0)
    {
      pipeline.AddSceneView(new SceneView(0, "Default", IsDefault: true, keys));
    }
  }

  public static int? AddGeometryMigrated(this ObjectsArtifactPipeline pipeline, string appId, Base geometry)
  {
#pragma warning disable CS0618 // Type or member is obsolete
    // A v2 Brep that leaked into displayValue, or a v3 Grasshopper BrepX/ExtrusionX/SubDX nested there: only its
    // (single) display mesh is encodable here — the caller emits the raw solid. Recurse so the mesh is checked too.
    if (geometry is Brep or RawEncodedObject)
    {
      List<Mesh>? displayMeshes = ((IDisplayValue<List<Mesh>?>)geometry).displayValue;
      return displayMeshes is { Count: > 0 } ? pipeline.AddGeometryMigrated(appId, displayMeshes[0]) : null;
    }
#pragma warning restore CS0618

    if (geometry is Arc a)
    {
      // V3 often sent non-normalized planes; Viewer 2.0 ignored them and recomputed from origin+start+end.
      // Viewer 3 (SGO) uses the plane, so normalize to remove one source of invalid arcs.
      a.plane.normal.Normalize();
      a.plane.xdir.Normalize();
      a.plane.ydir.Normalize();
    }
    else if (geometry is Mesh m)
    {
      //V2 would sometimes send nulls
      // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
      m.textureCoordinates ??= new();
      // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
      m.colors ??= new();
      // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
      m.vertexNormals ??= new();

      if (m.faces.Count > 0 && m.faces[0] < 3)
      {
        MigrateLegacyFaces(m);
      }

      // Vertex- or face-less meshes are not handled properly by the datgen (writes NaN into viewer.idx, which culls the WHOLE scene.)
      if (m is { vertices.Count: 0 } or { faces.Count: 0 })
      {
        return null;
      }
    }
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    else if (geometry is ICurve ic && ic.domain is null)
    {
      //v2 frequently sent null domains
      geometry["domain"] = Interval.UnitInterval;
    }

    if (geometry is Curve c)
    {
      // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
      if (c is { displayValue: null } cd)
      {
        //Detecting several models who have curves with no displayValue.
        //I only expect this from v2, so accepting this as a hack to approximate some form of displayValue
        cd.displayValue = new Polyline()
        {
          value = cd.points,
          units = cd.units,
          closed = cd.closed,
        };
      }

      if (c is { points: null })
      {
        return null;
      }
    }

    if (geometry is Arc { startPoint: null } or Arc { endPoint: null })
    {
      return null;
    }

    if (geometry is Surface or Vector or Plane or Spiral)
    {
      return null;
    }

    return pipeline.AddGeometry(appId, geometry);
  }

  private static void MigrateLegacyFaces(Mesh mesh)
  {
    List<int> faces = mesh.faces;
    int i = 0;
    while (i < faces.Count)
    {
      int vertexCount = faces[i] < 3 ? faces[i] + 3 : faces[i];
      faces[i] = vertexCount;
      i += vertexCount + 1;
    }
  }
}
