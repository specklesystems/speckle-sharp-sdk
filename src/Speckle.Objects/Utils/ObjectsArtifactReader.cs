#if NETSTANDARD2_0 || NET8_0_OR_GREATER
using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Sdk.Common;
using Speckle.Objects.Other;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>Options controlling how a Speckle 4.0 artefact bundle is reconstructed into a <see cref="Base"/> graph.</summary>
/// <param name="PreferSolids">When true (Rhino), an object that carries a raw 3dm <c>SOLID</c> blob is rebuilt as a
/// <see cref="RhinoObject"/> with <c>rawEncoding</c> set, so the connector bakes the real solid. When false (Revit,
/// which can't import 3dm), the solid is ignored and the object is rebuilt from its <c>DISPLAY</c> meshes only.</param>
public sealed record ArtifactReceiveOptions(bool PreferSolids);

/// <summary>
/// Maps a parsed Speckle 4.0 artefact <see cref="ArtefactBundle"/> back into a <see cref="Base"/>/<see
/// cref="Collection"/> graph that the v1 connector host builders consume — layers as nested collections, objects as
/// <see cref="DataObject"/>/<see cref="RhinoObject"/> with <c>displayValue</c> (SGEO-decoded geometry) + 3dm
/// <c>rawEncoding</c> solids + properties, and render-material / instance-definition proxies on root dynamic props.
/// Used by connectors that still receive through the v1 host-build path (e.g. Revit). Rhino instead bakes the bundle
/// directly via its dedicated artefact host builder and does NOT go through this reconstruction.
/// SGEO blobs decode via <see cref="SgeoDecoder"/>.
/// </summary>
public sealed class ObjectsArtifactReader
{
  // Proxy dynamic-prop keys — MUST match Speckle.Connectors.Common.Operations.ProxyKeys (the RootObjectUnpacker
  // reads these literals off the root). Hardcoded here to keep the reader connector-agnostic.
  private const string RenderMaterialProxiesKey = "renderMaterialProxies";
  private const string InstanceDefinitionProxiesKey = "instanceDefinitionProxies";

  public async Task<Base> ReadAsync(
    string bundleDir,
    ArtifactReceiveOptions options,
    CancellationToken cancellationToken
  )
  {
    var bundle = await ArtefactBundleReader.ReadAsync(bundleDir, cancellationToken).ConfigureAwait(false);
    return Build(bundle, options, cancellationToken);
  }

  /// <summary>Maps an already-parsed bundle into the <see cref="Base"/> graph (no IO).</summary>
  public Base Build(ArtefactBundle bundle, ArtifactReceiveOptions options, CancellationToken cancellationToken)
  {
    var nodes = bundle.Nodes;
    var rels = bundle.Relations;

    // ── collection (layer) tree ───────────────────────────────────────────────────────────────────────
    var (root, layerByNode) = BuildCollectionTree(nodes);

    // ── materials (MATERIAL nodes) ────────────────────────────────────────────────────────────────────
    var materialByNode = BuildMaterials(nodes);

    // reverse map geometryK -> owning objectK (from DISPLAY), used to attribute HAS_MATERIAL to objects.
    var objByGeom = rels.ObjectByGeometry();

    // ── build each object, wiring DISPLAY/SOLID/IN_COLLECTION/DISPLAY_INSTANCE ─────────────────────────
    foreach (var kv in bundle.ObjectAppIds)
    {
      cancellationToken.ThrowIfCancellationRequested();
      int objK = kv.Key;
      string appId = kv.Value;
      bundle.Properties.TryGetValue(objK, out var props);
      props ??= new Dictionary<string, object?>();

      Base built;
      if (
        rels.DisplayInstanceByObject.TryGetValue(objK, out int instNodeK)
        && nodes.TryGetValue(instNodeK, out var instNode)
      )
      {
        built = BuildInstanceProxy(appId, instNode);
      }
      else
      {
        built = BuildGeometryObject(appId, objK, props, bundle.Geometries, rels, options);
      }

      // place into its collection (layer); fall back to the root.
      var host =
        rels.CollectionByObject.TryGetValue(objK, out int collNodeK)
        && layerByNode.TryGetValue(collNodeK, out var layer)
          ? layer.elements
          : root.elements;
      host.Add(built);
    }

    // ── materials → objects (HAS_MATERIAL geometry->material, resolved to the owning object's appId) ───
    AttachMaterials(rels, objByGeom, bundle.ObjectAppIds, materialByNode, root);

    // ── instance definitions (DEFINITION nodes + DEFINES/DEFINES_INSTANCE) ────────────────────────────
    AttachInstanceDefinitions(nodes, rels, objByGeom, bundle.ObjectAppIds, root);

    root["units"] = bundle.Units;
    return root;
  }

  // ── collections (layers) ──────────────────────────────────────────────────────────────────────────────
  private static (Collection root, Dictionary<int, Collection> byNode) BuildCollectionTree(
    Dictionary<int, ArtefactNode> nodes
  )
  {
    var root = new Collection("Received model") { applicationId = "artifact-root", id = "artifact-root" };
    var byNode = new Dictionary<int, Collection>();
    foreach (var kv in nodes)
    {
      if (kv.Value.Kind == NodeKind.Container)
      {
        byNode[kv.Key] = new Layer(kv.Value.Name ?? "Layer")
        {
          applicationId = "coll-" + kv.Key,
          id = "coll-" + kv.Key,
        };
      }
    }
    // nest via parent (def_ref); roots (no parent) under the model root.
    foreach (var kv in byNode)
    {
      var node = nodes[kv.Key];
      if (node.DefRef is int parent && byNode.TryGetValue(parent, out var parentColl))
      {
        parentColl.elements.Add(kv.Value);
      }
      else
      {
        root.elements.Add(kv.Value);
      }
    }
    return (root, byNode);
  }

  // ── materials ─────────────────────────────────────────────────────────────────────────────────────────
  private static Dictionary<int, RenderMaterialProxy> BuildMaterials(Dictionary<int, ArtefactNode> nodes)
  {
    var map = new Dictionary<int, RenderMaterialProxy>();
    foreach (var kv in nodes)
    {
      var n = kv.Value;
      if (n.Kind != NodeKind.Material)
      {
        continue;
      }
      var material = new RenderMaterial
      {
        name = n.Name ?? "material",
        diffuse = n.Argb ?? unchecked((int)0xFFFFFFFF),
        opacity = n.Opacity ?? 1.0,
        metalness = n.Metalness ?? 0.0,
        roughness = n.Roughness ?? 1.0,
        applicationId = "mat-" + kv.Key,
      };
      map[kv.Key] = new RenderMaterialProxy
      {
        value = material,
        objects = new List<string>(),
        applicationId = "mat-" + kv.Key,
        id = "mat-" + kv.Key,
      };
    }
    return map;
  }

  private static void AttachMaterials(
    ArtefactRelations rels,
    Dictionary<int, int> objByGeom,
    Dictionary<int, string> objIdToApp,
    Dictionary<int, RenderMaterialProxy> materialByNode,
    Base root
  )
  {
    foreach (var kv in rels.MaterialByGeometry)
    {
      int geomK = kv.Key;
      int matNodeK = kv.Value;
      if (
        materialByNode.TryGetValue(matNodeK, out var proxy)
        && objByGeom.TryGetValue(geomK, out int objK)
        && objIdToApp.TryGetValue(objK, out var appId)
        && !proxy.objects.Contains(appId)
      )
      {
        proxy.objects.Add(appId);
      }
    }
    // NB: store as List<object> — the host-side RootObjectUnpacker reads proxies via `root[key] as List<object>`
    // (this is how the v1 deserializer materialises them). A List<Base> would fail that cast → no materials.
    var used = materialByNode.Values.Where(p => p.objects.Count > 0).Cast<object>().ToList();
    if (used.Count > 0)
    {
      root[RenderMaterialProxiesKey] = used;
    }
  }

  // ── instances ─────────────────────────────────────────────────────────────────────────────────────────
  private static Base BuildInstanceProxy(string appId, ArtefactNode instanceNode)
  {
    return new InstanceProxy
    {
      applicationId = appId,
      id = appId,
      definitionId = "def-" + (instanceNode.DefRef ?? -1),
      transform = ParseTransform(instanceNode.Transform),
      units = instanceNode.Units is { Length: > 0 } u ? u : Units.None,
      maxDepth = 0,
    };
  }

  private static void AttachInstanceDefinitions(
    Dictionary<int, ArtefactNode> nodes,
    ArtefactRelations rels,
    Dictionary<int, int> objByGeom,
    Dictionary<int, string> objIdToApp,
    Base root
  )
  {
    // List<object> so RootObjectUnpacker's `root[key] as List<object>` cast succeeds (see note in AttachMaterials).
    var proxies = new List<object>();
    foreach (var kv in nodes)
    {
      if (kv.Value.Kind != NodeKind.Definition)
      {
        continue;
      }
      int defNodeK = kv.Key;
      var members = new List<string>();
      // DEFINES def -> geometry; map geometry back to its owning object's applicationId.
      if (rels.DefinesByDefinition.TryGetValue(defNodeK, out var geomKs))
      {
        foreach (var geomK in geomKs)
        {
          if (
            objByGeom.TryGetValue(geomK, out int objK)
            && objIdToApp.TryGetValue(objK, out var appId)
            && !members.Contains(appId)
          )
          {
            members.Add(appId);
          }
        }
      }
      proxies.Add(
        new InstanceDefinitionProxy
        {
          applicationId = "def-" + defNodeK,
          id = "def-" + defNodeK,
          name = kv.Value.Name ?? ("Definition " + defNodeK),
          objects = members,
          maxDepth = 0,
        }
      );
    }
    if (proxies.Count > 0)
    {
      root[InstanceDefinitionProxiesKey] = proxies;
    }
  }

  // ── per-object geometry build ───────────────────────────────────────────────────────────────────────
  private Base BuildGeometryObject(
    string appId,
    int objK,
    Dictionary<string, object?> props,
    Dictionary<int, ArtefactGeometry> geometries,
    ArtefactRelations rels,
    ArtifactReceiveOptions options
  )
  {
    string name = Scalar(props, "name", appId);
    string units = Scalar(props, "units", Units.None);
    string type = Scalar(props, "type", Scalar(props, "speckle_type", "object"));

    // DISPLAY meshes (decode SGEO), ordered by ord.
    var displays = new List<Base>();
    if (rels.DisplayByObject(objK) is { } displayEdges)
    {
      foreach (var e in displayEdges.OrderBy(x => x.Ord))
      {
        if (geometries.TryGetValue(e.Dst, out var g) && TryDecode(g) is { } geom)
        {
          // Stamp the display geometry with the owning object's applicationId so the host material baker
          // (which keys per displayValue item on the mesh path) can resolve HAS_MATERIAL → object → material.
          geom.applicationId = appId;
          displays.Add(geom);
        }
      }
    }

    // SOLID 3dm blob (Rhino only): rebuild as a RhinoObject with rawEncoding so the connector bakes the solid.
    if (options.PreferSolids && rels.SolidByObject.TryGetValue(objK, out var solidKs))
    {
      foreach (var solidK in solidKs)
      {
        if (geometries.TryGetValue(solidK, out var g) && g.Type == RawEncodingFormats.RHINO_3DM)
        {
          return new RhinoObject
          {
            name = name,
            type = type,
            units = units,
            displayValue = displays,
            properties = props,
            applicationId = appId,
            // received artefact objects aren't serialized (no content hash) — use the applicationId as a stable,
            // non-null id so the receive conversion-report path (ReceiveConversionResult.source.id) is satisfied.
            id = appId,
            rawEncoding = new RawEncoding
            {
              format = RawEncodingFormats.RHINO_3DM,
              contents = Convert.ToBase64String(g.Content),
            },
          };
        }
      }
    }

    return new DataObject
    {
      name = name,
      displayValue = displays,
      properties = props,
      applicationId = appId,
      id = appId,
    };
  }

  private static string Scalar(Dictionary<string, object?> props, string key, string fallback) =>
    props.TryGetValue(key, out var v) && v is string s && s.Length > 0 ? s : fallback;

  private static Base? TryDecode(ArtefactGeometry entry)
  {
    try
    {
      return entry.IsSgeo ? SgeoDecoder.Decode(entry.Content) : null;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      return null;
    }
  }

  private static Matrix4x4 ParseTransform(string? csv)
  {
    var d = new double[16];
    if (csv is { Length: > 0 } text)
    {
      var parts = text.Split(',');
      for (int i = 0; i < 16 && i < parts.Length; i++)
      {
        double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out d[i]);
      }
    }
    else
    {
      d[0] = d[5] = d[10] = d[15] = 1.0; // identity
    }
    return new Matrix4x4(
      d[0],
      d[1],
      d[2],
      d[3],
      d[4],
      d[5],
      d[6],
      d[7],
      d[8],
      d[9],
      d[10],
      d[11],
      d[12],
      d[13],
      d[14],
      d[15]
    );
  }
}
#endif
