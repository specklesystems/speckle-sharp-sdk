namespace Speckle.Sdk.Artifacts.Harness;

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
  public int Colors;
  public int HasColorEdges;
  public int Levels;
  public int OnLevelEdges;
  public int Collections;
  public int InCollectionEdges;
  public int DefinitionGeometries;
  public int DefinitionInstances;
  public int DefinesInstanceEdges;
  public int MeshAtomics;
  public int InstanceAtomics;

  // Raw (non-SGEO) native solid blobs: SOLID edges on atomic objects, DEFINES-linked solids on def members.
  public int Solids;
  public int DefinitionSolids;

  // Proxy refs whose target appId isn't in the graph — skipped rather than minting a phantom K.
  public int SkippedDefines;
  public int SkippedMaterial;
  public int SkippedColor;
  public int SkippedLevel;
  public int SkippedDangling => SkippedDefines + SkippedMaterial + SkippedColor + SkippedLevel;
  public readonly List<string> Notes = new();

  public override string ToString() =>
    $"""
      objects={Objects} (meshAtomic={MeshAtomics} instAtomic={InstanceAtomics})  geometries={Geometries} (defGeom={DefinitionGeometries})
      edges: DISPLAY={DisplayEdges} DISPLAY_INSTANCE={DisplayInstanceEdges} SUBELEMENT={SubelementEdges} SOLID={Solids} (defSolid={DefinitionSolids})
             DEFINES={DefinesEdges} DEFINES_INSTANCE={DefinesInstanceEdges} HAS_MATERIAL={HasMaterialEdges} HAS_COLOR={HasColorEdges} ON_LEVEL={OnLevelEdges} IN_COLLECTION={InCollectionEdges}
      nodes: DEFINITION={Definitions} INSTANCE(def)={DefinitionInstances} MATERIAL={Materials} COLOR={Colors} LEVEL={Levels} COLLECTION={Collections}
      skipped (ref not in graph): {SkippedDangling}  (DEFINES={SkippedDefines} HAS_MATERIAL={SkippedMaterial} HAS_COLOR={SkippedColor} ON_LEVEL={SkippedLevel})
      """;
}
