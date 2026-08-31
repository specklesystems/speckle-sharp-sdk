using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Objects.Utils;

/// <summary>Options controlling how a Speckle 4.0 artefact bundle is reconstructed into a <see cref="Base"/> graph.</summary>
/// <param name="PreferSolids">When true (Rhino), an object that carries a raw 3dm <c>SOLID</c> blob is rebuilt as a
/// <see cref="RhinoObject"/> with <c>rawEncoding</c> set, so the connector bakes the real solid. When false (Revit,
/// which can't import 3dm), the solid is ignored and the object is rebuilt from its <c>DISPLAY</c> meshes only.</param>
/// <param name="CompleteCarriage">The complete-data-carriage floor (ENG-9301), the SDK default: objects without
/// their own DISPLAY geometry — definition members, placement carriers, non-geometric property carriers — surface
/// in the tree with their properties (members with their DEFINES geometry as <c>displayValue</c>, joined via the
/// (definition, member-ordinal) vocabulary), and a geometry decode failure throws instead of silently dropping the
/// mesh. <see langword="false"/> is the connector-bake profile: the v1 hosts' shape, where such carriers are
/// skipped and decode is best-effort.</param>
public sealed record ArtifactReceiveOptions(bool PreferSolids, bool CompleteCarriage = true);

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

  // ENG-8947: the v1 (net48) reconstruction path rebuilds the reference-point transform from the bundle meta offset
  // and lifts it onto the reconstructed root as the metadata dict Speckle.Connectors...RevitShared.RevitHostObjectBuilder
  // expects (a FEET matrix), so its untouched reference-point composition just works. MUST match
  // Speckle.Connectors.Common.Operations.RootKeys.REFERENCE_POINT_TRANSFORM; hardcoded here (like the proxy keys).
  private const string ReferencePointTransformKey = "referencePointTransform";

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

    // ENG-9101: group by Src once so an object with several placements (e.g. a Revit railing -> many balusters)
    // yields one InstanceProxy per edge instead of the last one winning (DisplayInstanceByObject is a last-wins map).
    var placementsByObject = rels.DisplayInstanceEdges.GroupBy(e => e.Src).ToDictionary(g => g.Key, g => g.ToList());

    // ENG-9301: the (definition, member-ordinal) join — a member object's geometry hangs on DEFINES with the same
    // ordinal its DEFINES_MEMBER edge carries, never on DISPLAY.
    var memberGeomsByObject = BuildMemberGeometryJoin(rels);

    // ── build each object, wiring DISPLAY/SOLID/IN_COLLECTION/DISPLAY_INSTANCE ─────────────────────────
    foreach (var kv in bundle.ObjectAppIds)
    {
      cancellationToken.ThrowIfCancellationRequested();
      int objK = kv.Key;
      string appId = kv.Value;

      var host =
        rels.CollectionByObject.TryGetValue(objK, out int collNodeK)
        && layerByNode.TryGetValue(collNodeK, out var layer)
          ? layer.elements
          : root.elements;

      if (placementsByObject.TryGetValue(objK, out var placements))
      {
        // one InstanceProxy per placement edge; only the first keeps the object's own appId, the rest get a
        // synthetic suffix so none collide (they're independent placements, not the same instance).
        for (int i = 0; i < placements.Count; i++)
        {
          if (!nodes.TryGetValue(placements[i].Dst, out var instNode))
          {
            continue;
          }
          string placementAppId = i == 0 ? appId : $"{appId}-instance-{i}";
          host.Add(BuildInstanceProxy(placementAppId, instNode));
        }
        continue;
      }

      bundle.Properties.TryGetValue(objK, out var props);
      props ??= new Dictionary<string, object?>();
      // ENG-9302: type-scoped params (Parameters.Type/System Type Parameters, the compound Structure) live once per
      // type in type_eav; the v3 tree carried them on every element, so merge them back for the legacy consumers.
      if (bundle.TypePropertiesByObject.TryGetValue(objK, out var typeProps))
      {
        props = MergeTypeScoped(typeProps, props);
      }
      var built = BuildGeometryObject(appId, objK, props, bundle.Geometries, rels, options);
      if (built is null && options.CompleteCarriage)
      {
        if (rels.PlacesByObject.ContainsKey(objK))
        {
          // a nested-placement member: it surfaces as the InstanceProxy AttachInstanceDefinitions emits under its
          // real applicationId — building a DataObject here too would duplicate it.
          continue;
        }
        // ENG-9301 complete carriage: a definition member (geometry via the member-ordinal join) or a pure property
        // carrier (Level/Room/no-DISPLAY element). Both carry the data layer a script reads; the v1 unpacker pulls
        // members out of atomic baking via the definition proxies' member lists, so nothing bakes twice.
        built = BuildCarrierObject(appId, objK, props, memberGeomsByObject, bundle.Geometries, options);
      }
      if (built is null)
      {
        // Connector-bake profile: non-geometric objects (no DISPLAY edges, no accepted SOLID) are skipped — the v1
        // host builders have no path for an empty-displayValue DataObject (Levels/Rooms travel as proxies there).
        continue;
      }

      host.Add(built);
    }

    // ── materials → objects (HAS_MATERIAL geometry->material, resolved to the owning object's appId) ───
    AttachMaterials(rels, objByGeom, bundle.ObjectAppIds, materialByNode, root);

    // ── instance definitions (DEFINITION nodes + DEFINES/DEFINES_INSTANCE) ────────────────────────────
    AttachInstanceDefinitions(
      nodes,
      rels,
      objByGeom,
      bundle.ObjectAppIds,
      bundle.Geometries,
      root,
      options,
      layerByNode
    );

    // ENG-8947/8808: rebuild the reference-point transform from the bundle meta offset and lift it onto the root so
    // the v1 Revit host builder can undo/redo it (translation kinds only; the offset is in display units).
    if (BuildReferencePointRootValue(bundle) is { } refPointRootValue)
    {
      root[ReferencePointTransformKey] = refPointRootValue;
    }

    root["units"] = bundle.Units;
    return root;
  }

  // ENG-8947: rebuild the v1 root reference-point transform from the eav.model record
  // (referencePoint.kind/.transform/.units — the former meta.reference_point_* columns are removed from the
  // spec). transform is the FULL rigid transform, 16 row-major doubles in referencePoint.units (InstanceProxy
  // layout, translation at 3/7/11); re-emitted here in the legacy root layout
  // ReferencePointHelper.GetTransformFromRootObject expects (basis columns first, translation at 12–14,
  // internal feet — the unit v1 applies the transform in).
  private static Dictionary<string, object>? BuildReferencePointRootValue(ArtefactBundle bundle)
  {
    if (
      !bundle.ModelProperties.TryGetValue("referencePoint", out var rpObj)
      || rpObj is not Dictionary<string, object?> rp
      || !rp.TryGetValue("transform", out var tObj)
      || tObj is not string transformCsv
    )
    {
      return null;
    }
    var parts = transformCsv.Split(',');
    if (parts.Length != 16)
    {
      return null;
    }
    var d = new double[16];
    for (int i = 0; i < 16; i++)
    {
      if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out d[i]))
      {
        return null;
      }
    }
    string units = rp.TryGetValue("units", out var uObj) && uObj is string u && u.Length > 0 ? u : bundle.Units;
    double toFeet = Units.GetConversionFactor(units, Units.Feet);
    var m = new double[]
    {
      d[0],
      d[4],
      d[8],
      0,
      d[1],
      d[5],
      d[9],
      0,
      d[2],
      d[6],
      d[10],
      0,
      d[3] * toFeet,
      d[7] * toFeet,
      d[11] * toFeet,
      1,
    };
    return new Dictionary<string, object> { ["transform"] = m };
  }

  // ── collections (layers) ──────────────────────────────────────────────────────────────────────────────
  private static (Collection root, Dictionary<int, Collection> byNode) BuildCollectionTree(
    Dictionary<int, ArtefactNode> nodes
  )
  {
    var root = new Collection
    {
      name = "Received model",
      applicationId = "artifact-root",
      id = "artifact-root",
    };
    var byNode = new Dictionary<int, Collection>();
    foreach (var kv in nodes)
    {
      if (kv.Value.Kind == NodeKind.Container)
      {
        byNode[kv.Key] = new Layer
        {
          name = kv.Value.Name ?? "Layer",
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
      if (n.Emissive is int emissive)
      {
        material.emissive = emissive;
      }
      if (n.Ior is double ior)
      {
        // dynamic prop, matching the v1 RhinoMaterialUnpacker convention so receive converters find it where v1 put it
        material["ior"] = ior;
      }
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

  // Builds an InstanceDefinitionProxy per DEFINITION node. A DEFINES member is a real object only when one
  // independently displays that geometry — the exception, not the rule: shared block/family geometry is normally
  // referenced ONLY via DEFINES, with no owning scene object at all (the direct-bake path never needs one either, it
  // decodes geometry straight by index). Members without an owner are synthesized instead — a DataObject wrapping the
  // decoded geometry, or an InstanceProxy for a DEFINES_INSTANCE nested placement — and added to the root graph so
  // the v1 unpacker's traversal can find them by applicationId. Mirrors
  // RhinoHostObjectArtefactBuilder.BuildDefinitions' depth-first nested-block handling.
  private static void AttachInstanceDefinitions(
    Dictionary<int, ArtefactNode> nodes,
    ArtefactRelations rels,
    Dictionary<int, int> objByGeom,
    Dictionary<int, string> objIdToApp,
    Dictionary<int, ArtefactGeometry> geometries,
    Collection root,
    ArtifactReceiveOptions options,
    Dictionary<int, Collection> layerByNode
  )
  {
    // ENG-9301: under complete carriage, real member objects (DEFINES_MEMBER) already sit in the tree with their
    // geometry and properties — the proxies reference them by applicationId instead of synthesizing wrappers.
    var memberByDefOrd = new Dictionary<(int def, int ord), int>();
    foreach (var kv in rels.MemberObjectsByDefinition)
    {
      var ords = rels.MemberOrdByDefinition[kv.Key];
      for (int m = 0; m < kv.Value.Count; m++)
      {
        memberByDefOrd[(kv.Key, ords[m])] = kv.Value[m];
      }
    }
    var placesMemberByInstNode = new Dictionary<int, int>();
    foreach (var kv in rels.PlacesByObject)
    {
      placesMemberByInstNode[kv.Value] = kv.Key;
    }

    // RevitFamilyBaker bakes definitions deepest-first (OrderByDescending(maxDepth)) so a parent can reference an
    // already-baked child via PlaceNestedInstance — mirrors RhinoInstanceUnpacker/GrasshopperBlockPacker's send-side
    // depth tracking. A definition reachable via multiple nesting paths takes the deepest (never bake a shared child
    // before every parent that nests it has been accounted for).
    var depthByDefNode = ComputeDefinitionDepths(nodes, rels);

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
      // DEFINES def -> geometry; prefer the owning object's applicationId, else synthesize a member wrapping the
      // decoded geometry directly.
      if (rels.DefinesByDefinition.TryGetValue(defNodeK, out var geomKs))
      {
        var geomOrds = rels.DefinesOrdByDefinition[defNodeK];
        for (int gi = 0; gi < geomKs.Count; gi++)
        {
          int geomK = geomKs[gi];
          // complete carriage: the (definition, ordinal) join names the owning member — the real object is already
          // in the tree (BuildCarrierObject) with this geometry as its displayValue.
          if (
            options.CompleteCarriage
            && memberByDefOrd.TryGetValue((defNodeK, geomOrds[gi]), out int memberObjK)
            && objIdToApp.TryGetValue(memberObjK, out var memberAppId)
          )
          {
            if (!members.Contains(memberAppId))
            {
              members.Add(memberAppId);
            }
            continue;
          }
          if (objByGeom.TryGetValue(geomK, out int objK) && objIdToApp.TryGetValue(objK, out var appId))
          {
            if (!members.Contains(appId))
            {
              members.Add(appId);
            }
            continue;
          }
          if (geometries.TryGetValue(geomK, out var g) && TryDecode(g, options.CompleteCarriage) is { } geom)
          {
            string geoAppId = "def-geo-" + geomK.ToString(CultureInfo.InvariantCulture);
            if (!members.Contains(geoAppId))
            {
              geom.applicationId = geoAppId;
              root.elements.Add(
                new DataObject
                {
                  name = "geometry",
                  displayValue = new List<Base> { geom },
                  properties = new Dictionary<string, object?>(),
                  applicationId = geoAppId,
                  id = geoAppId,
                }
              );
              members.Add(geoAppId);
            }
          }
        }
      }
      // DEFINES_INSTANCE def -> INSTANCE node: a nested block/family placement inside this definition.
      if (rels.DefinesInstanceByDefinition.TryGetValue(defNodeK, out var nestedInstNodeKs))
      {
        foreach (var instNodeK in nestedInstNodeKs)
        {
          if (!nodes.TryGetValue(instNodeK, out var nestedInstNode))
          {
            continue;
          }
          // complete carriage: the PLACES member names this nested placement — surface the proxy under the member's
          // real applicationId, in the member's own collection, instead of a synthetic root entry.
          string nestedAppId;
          var nestedHost = root.elements;
          if (
            options.CompleteCarriage
            && placesMemberByInstNode.TryGetValue(instNodeK, out int placesMemberK)
            && objIdToApp.TryGetValue(placesMemberK, out var placesAppId)
          )
          {
            nestedAppId = placesAppId;
            if (
              rels.CollectionByObject.TryGetValue(placesMemberK, out int collNodeK)
              && layerByNode.TryGetValue(collNodeK, out var memberLayer)
            )
            {
              nestedHost = memberLayer.elements;
            }
          }
          else
          {
            nestedAppId = "nested-inst-" + instNodeK.ToString(CultureInfo.InvariantCulture);
          }
          nestedHost.Add(BuildInstanceProxy(nestedAppId, nestedInstNode));
          if (!members.Contains(nestedAppId))
          {
            members.Add(nestedAppId);
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
          // TryGetValue, not GetValueOrDefault: the latter is unavailable on netstandard2.0 (the net48 plugin build).
          maxDepth = depthByDefNode.TryGetValue(defNodeK, out int defDepth) ? defDepth : 0,
        }
      );
    }
    if (proxies.Count > 0)
    {
      root[InstanceDefinitionProxiesKey] = proxies;
    }
  }

  // Depth-from-scene-root per DEFINITION node: 0 for one placed directly (DISPLAY_INSTANCE), +1 per level of
  // DEFINES_INSTANCE nesting below. A definition reachable via several paths takes the deepest — RevitFamilyBaker
  // bakes highest-maxDepth first and must never bake a shared child before all its parents are accounted for.
  // Cycle-guarded per DFS branch; anything unreachable from a top-level placement falls back to 0.
  private static Dictionary<int, int> ComputeDefinitionDepths(
    Dictionary<int, ArtefactNode> nodes,
    ArtefactRelations rels
  )
  {
    var depth = new Dictionary<int, int>();

    void Propagate(int defNodeK, int d, HashSet<int> onStack)
    {
      if (!onStack.Add(defNodeK))
      {
        return; // cycle — never re-enter an ancestor of itself
      }
      if (!depth.TryGetValue(defNodeK, out var existing) || existing < d)
      {
        depth[defNodeK] = d;
        if (rels.DefinesInstanceByDefinition.TryGetValue(defNodeK, out var nestedInstNodeKs))
        {
          foreach (var instNodeK in nestedInstNodeKs)
          {
            if (nodes.TryGetValue(instNodeK, out var nestedInst) && nestedInst.DefRef is int childDefNodeK)
            {
              Propagate(childDefNodeK, d + 1, onStack);
            }
          }
        }
      }
      onStack.Remove(defNodeK);
    }

    foreach (var edge in rels.DisplayInstanceEdges)
    {
      if (nodes.TryGetValue(edge.Dst, out var instNode) && instNode.DefRef is int defNodeK)
      {
        Propagate(defNodeK, 0, new HashSet<int>());
      }
    }
    return depth;
  }

  // ── per-object geometry build ───────────────────────────────────────────────────────────────────────
  // Returns null for a non-geometric object (no DISPLAY edges, no accepted SOLID) — the caller skips it entirely
  // rather than adding an empty-displayValue DataObject the v1 converter pipeline can't handle.
  private Base? BuildGeometryObject(
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
        if (geometries.TryGetValue(e.Dst, out var g) && TryDecode(g, options.CompleteCarriage) is { } geom)
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

    if (displays.Count == 0)
    {
      // No DISPLAY geometry and no accepted SOLID: a non-geometric element (Level/Room/etc.) recorded only for its
      // properties/relationship edges, or a definition-source object whose geometry is wired via DEFINES rather than
      // DISPLAY. Mirrors RevitHostObjectArtefactBuilder.BakeAtomic's skip for the same category of object.
      return null;
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

  // Deep-merges the shared per-type dictionary under the instance one, INSTANCE winning on a leaf collision (the
  // same precedence the hosts apply, ENG-9136). Copy-on-write: the result is a fresh dictionary per object, but
  // subtrees that exist only on the type side are shared by reference across every object of the type — the
  // one-parse-per-type property (ENG-9136) survives, and the shared type dictionaries are never mutated.
  internal static Dictionary<string, object?> MergeTypeScoped(
    Dictionary<string, object?> typeProps,
    Dictionary<string, object?> instanceProps
  )
  {
    var merged = new Dictionary<string, object?>(instanceProps);
    foreach (var kv in typeProps)
    {
      if (!merged.TryGetValue(kv.Key, out var existing))
      {
        merged[kv.Key] = kv.Value; // type-only subtree: share the per-type instance
      }
      else if (existing is Dictionary<string, object?> instChild && kv.Value is Dictionary<string, object?> typeChild)
      {
        merged[kv.Key] = MergeTypeScoped(typeChild, instChild);
      }
      // scalar (or shape) conflict: instance value stays
    }
    return merged;
  }

  // objK → the geometry Ks its DEFINES_MEMBER ordinal claims via DEFINES on the same (definition, ordinal).
  private static Dictionary<int, List<int>> BuildMemberGeometryJoin(ArtefactRelations rels)
  {
    var byObject = new Dictionary<int, List<int>>();
    foreach (var kv in rels.MemberObjectsByDefinition)
    {
      int def = kv.Key;
      var ords = rels.MemberOrdByDefinition[def];
      if (!rels.DefinesByDefinition.TryGetValue(def, out var geomKs))
      {
        continue;
      }
      var geomOrds = rels.DefinesOrdByDefinition[def];
      for (int m = 0; m < kv.Value.Count; m++)
      {
        for (int g = 0; g < geomKs.Count; g++)
        {
          if (geomOrds[g] != ords[m])
          {
            continue;
          }
          if (!byObject.TryGetValue(kv.Value[m], out var list))
          {
            byObject[kv.Value[m]] = list = new List<int>();
          }
          list.Add(geomKs[g]);
        }
      }
    }
    return byObject;
  }

  // ENG-9301: a no-DISPLAY carrier under complete carriage — a definition member (its DEFINES geometry becomes
  // displayValue; raw solids stay out, they are the connector profile's concern) or a pure property carrier
  // (empty displayValue, the properties are the payload).
  private static DataObject BuildCarrierObject(
    string appId,
    int objK,
    Dictionary<string, object?> props,
    Dictionary<int, List<int>> memberGeomsByObject,
    Dictionary<int, ArtefactGeometry> geometries,
    ArtifactReceiveOptions options
  )
  {
    var displays = new List<Base>();
    if (memberGeomsByObject.TryGetValue(objK, out var geomKs))
    {
      foreach (var geomK in geomKs)
      {
        if (geometries.TryGetValue(geomK, out var g) && TryDecode(g, options.CompleteCarriage) is { } geom)
        {
          geom.applicationId = appId;
          displays.Add(geom);
        }
      }
    }
    return new DataObject
    {
      name = Scalar(props, "name", appId),
      displayValue = displays,
      properties = props,
      applicationId = appId,
      id = appId,
    };
  }

  private static string Scalar(Dictionary<string, object?> props, string key, string fallback) =>
    props.TryGetValue(key, out var v) && v is string s && s.Length > 0 ? s : fallback;

  private static Base? TryDecode(ArtefactGeometry entry, bool loud = false)
  {
    try
    {
      // a non-SGEO blob (raw 3dm) in a display lane is simply not display geometry — never an error
      return entry.IsSgeo ? SgeoDecoder.Decode(entry.Content) : null;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      if (loud)
      {
        // ENG-9301: under the SDK profile a decode failure is data loss the caller must see, never a silent drop.
        throw new Speckle.Sdk.SpeckleException($"SGEO geometry failed to decode ({entry.Content.Length} bytes).", ex);
      }
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
