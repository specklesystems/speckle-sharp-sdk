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
    if (geometry is Arc a)
    {
      // V3 often sent non-normalized planes; Viewer 2.0 ignored them and recomputed from origin+start+end.
      // Viewer 3 (SGO) uses the plane, so normalize to remove one source of invalid arcs.
      a.plane.normal.Normalize();
      a.plane.xdir.Normalize();
      a.plane.ydir.Normalize();
    }
    else if (geometry is Mesh m && m.faces.Count > 0 && m.faces[0] < 3)
    {
      MigrateLegacyFaces(m);
    }
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    else if (geometry is ICurve c && c.domain is null)
    {
      //v2 frequently sent null domains
      geometry["domain"] = Interval.UnitInterval;
    }

    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    if (geometry is Curve { displayValue: null } cd)
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

#pragma warning disable CS0618 // Type or member is obsolete
    if (geometry is Brep b)
    {
      //Never considered valid, but some breps may have leaked as displayValues in v2.
      //Safe to assume v2 breps only have 1 displayValue mesh
      // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
      if (b.displayValue is null || b.displayValue.Count <= 0)
      {
        return null;
      }
      geometry = b.displayValue[0];
    }

    if (geometry is Surface or Vector or Plane or Brep or null)
    {
      return null;
    }
#pragma warning restore CS0618 // Type or member is obsolete

    // Vertex- or face-less meshes are not handled properly by the datgen (writes NaN into viewer.idx, which culls the WHOLE scene.)
    if (geometry is Mesh { vertices.Count: 0 } or Mesh { faces.Count: 0 })
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
