namespace Speckle.Sdk.BundleMigrator.Migration;

internal sealed class Stats
{
  public int Objects;
  public int Geometries;
  public int DisplayEdges;
  public int DisplayInstanceEdges;
  public int SubelementEdges;
  public int Definitions;
  public int DefinesEdges;
  public int Materials;
  public int HasMaterialEdges;
  public int ObjectHasMaterialEdges;
  public int NodeHasMaterialEdges;
  public int Colors;
  public int HasColorEdges;
  public int ObjectHasColorEdges;
  public int NodeHasColorEdges;

  // AutoCAD ByBlock placeholder colour proxies (source="block"), deliberately unbound.
  public int ByBlockColorProxies;
  public int Levels;
  public int OnLevelEdges;
  public int Collections;
  public int InCollectionEdges;

  // Grasshopper data-tree topology strings carried onto collection nodes (nodes.gh_topology).
  public int GhTopologies;
  public int Groups;
  public int InGroupEdges;
  public int DefinitionInstances;
  public int DefinesInstanceEdges;
  public int DefinesMemberEdges;
  public int PlacesEdges;
  public int StandaloneMeshes;
  public int StandalonePlacements;

  // Revit topology edges derived from the flat v3 property keys (room/space/fromRoom/toRoom/parent).
  public int InRoomEdges;
  public int ConnectsToEdges;
  public int HostSubelementEdges;

  // Federation tier: linked-model collections retagged as CONTAINER(subtype=Model).
  public int Models;
  public int InModelEdges;

  // Raw solid blobs (3dm/sat), standalone and definition-member alike.
  public int Solids;

  // Named viewpoints migrated from the source graph's `views`.
  public int CameraViews;

  // Root `referencePointTransform` re-emitted as referencePoint.* eav.model rows (ENG-9060).
  public int ReferencePoints;

  // v3 Civil3D root `propertySetDefinitions` re-emitted as eav.property_set_definitions rows (ENG-9062).
  public int PropertySets;
  public int PropertySetFields;

  // Distinct Revit type keys ({linkSuffix}|{category}|{family}|{type}) candidates for types/type_eav (ENG-9009).
  public int RevitTypeKeys;

  // v3 CSi root `analysisResults` flattened into structural_results, unit scalars into eav.model (ENG-9076).
  public int StructuralResultRows;
  public int StructuralElmFallbacks; // element name didn't resolve to a sent object; its raw name rides `location`
  public int SkippedResultTypes; // analysisResults keys with no structural_results mapping (e.g. TSD) — named in Notes
  public int ModelUnitRows;

  // Proxy refs whose target appId isn't in the graph — skipped rather than minting a phantom K.
  public int SkippedDefines;
  public int SkippedMaterial;
  public int SkippedColor;
  public int SkippedLevel;
  public int SkippedGroup;
  public int SkippedRoom;
  public int SkippedConnects;
  public int SkippedHostParent;
  public int SkippedDangling =>
    SkippedDefines
    + SkippedMaterial
    + SkippedColor
    + SkippedLevel
    + SkippedGroup
    + SkippedRoom
    + SkippedConnects
    + SkippedHostParent;
  public readonly List<string> Notes = new();

  public override string ToString() =>
    $"""
      objects={Objects} (standaloneMeshes={StandaloneMeshes} standalonePlacements={StandalonePlacements})  geometries={Geometries} solids={Solids}
      edges: DISPLAY={DisplayEdges} DISPLAY_INSTANCE={DisplayInstanceEdges} SUBELEMENT={SubelementEdges}
             DEFINES={DefinesEdges} DEFINES_INSTANCE={DefinesInstanceEdges} DEFINES_MEMBER={DefinesMemberEdges} PLACES={PlacesEdges} HAS_MATERIAL={HasMaterialEdges} HAS_COLOR={HasColorEdges} ON_LEVEL={OnLevelEdges} IN_COLLECTION={InCollectionEdges} IN_GROUP={InGroupEdges}
             OBJECT_HAS_MATERIAL={ObjectHasMaterialEdges} OBJECT_HAS_COLOR={ObjectHasColorEdges} NODE_HAS_MATERIAL={NodeHasMaterialEdges} NODE_HAS_COLOR={NodeHasColorEdges}
             IN_ROOM={InRoomEdges} CONNECTS_TO={ConnectsToEdges} SUBELEMENT(host)={HostSubelementEdges} IN_MODEL={InModelEdges}
      nodes: DEFINITION={Definitions} INSTANCE(def)={DefinitionInstances} MATERIAL={Materials} COLOR={Colors} LEVEL={Levels} COLLECTION={Collections} (ghTopology={GhTopologies}) GROUP={Groups} CAMERA_VIEW={CameraViews} MODEL={Models}
      structuralResults: rows={StructuralResultRows} elmFallback={StructuralElmFallbacks} skippedTypes={SkippedResultTypes} unitRows={ModelUnitRows}
      referencePoints={ReferencePoints}  propertySets={PropertySets} (fields={PropertySetFields})  revitTypeKeys={RevitTypeKeys}
      skipped (ref not in graph): {SkippedDangling}  (DEFINES={SkippedDefines} HAS_MATERIAL={SkippedMaterial} HAS_COLOR={SkippedColor} ON_LEVEL={SkippedLevel} IN_GROUP={SkippedGroup} IN_ROOM={SkippedRoom} CONNECTS_TO={SkippedConnects} SUBELEMENT(host)={SkippedHostParent})  byBlockColorProxies={ByBlockColorProxies}
      """;
}
