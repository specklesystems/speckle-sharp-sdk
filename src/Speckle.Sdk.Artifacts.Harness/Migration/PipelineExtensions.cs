using Speckle.Objects;
using Speckle.Objects.Geometry;
using Speckle.Objects.Primitive;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Artifacts.Harness.Migration;

internal static class PipelineExtensions
{
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

    if (geometry is Surface or Vector or Plane or Brep)
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
