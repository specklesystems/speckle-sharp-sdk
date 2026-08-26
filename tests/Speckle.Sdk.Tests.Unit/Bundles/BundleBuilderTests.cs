using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Bundles;

/// <summary>Write with <see cref="BundleBuilder"/>, read back through the Receive3 façade: the two sides must agree.</summary>
public sealed class BundleBuilderTests : IDisposable
{
  private static readonly SpeckleApplication s_app = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.0",
    Slug = "test",
    SpeckleVersion = "0.0.0",
  };

  private readonly string _dir = Path.Combine(Path.GetTempPath(), "BundleBuilderTests", Guid.NewGuid().ToString("N"));

  private static Mesh Tri(double dx = 0) =>
    new()
    {
      vertices = [dx, 0, 0, dx + 1, 0, 0, dx, 1, 0],
      faces = [3, 0, 1, 2],
      units = "m",
    };

  private async Task<Model> BuildAndRead(Action<BundleBuilder> populate)
  {
    BundleFiles files;
    using (var b = new BundleBuilder(s_app, "m", _dir))
    {
      populate(b);
      files = b.Build();
    }
    Assert.All(files.Files, f => Assert.StartsWith("bundle.", Path.GetFileName(f), StringComparison.Ordinal));
    var bundle = await ArtefactBundleReader.ReadAsync(_dir, ArtefactReadOptions.Columnar, CancellationToken.None);
    return new Model("p", "m", "v", _dir, files.Files, bundle, geometryDownloaded: true, NullLogger.Instance);
  }

  [Fact]
  public async Task Objects_Properties_Collections_RoundTrip()
  {
    using var model = await BuildAndRead(b =>
    {
      var walls = b.GetOrAddCollection(["Level 1", "Walls"], subtype: "Category");
      var wall = b.GetOrAddObject(
        "wall-1",
        walls,
        new Dictionary<string, object?>
        {
          ["Constraints"] = new Dictionary<string, object?> { ["Base Offset"] = 0.5 },
          ["Identity Data"] = new Dictionary<string, object?> { ["Mark"] = "W-01" },
        },
        name: "Basic Wall",
        speckleType: "Objects.Data.DataObject",
        sourceType: "Walls"
      );
      wall.AddGeometry(Tri());
      b.GetOrAddObject("door-1", walls, new Dictionary<string, object?> { ["Width"] = 0.9 }, name: "Door");
      // interning: same id twice is one object
      Assert.Same(wall, b.GetOrAddObject("wall-1", walls, null));
    });

    Assert.Equal("m", model.Units);
    Assert.Equal(2, model.Objects.Count);
    var wall = model.ObjectByApplicationId("wall-1")!;
    Assert.Equal("Basic Wall", wall.Name);
    Assert.Equal(0.5, wall.GetDouble("Constraints.Base Offset"));
    Assert.Equal("W-01", wall.GetString("Identity Data.Mark"));
    Assert.Equal("Objects.Data.DataObject", wall.GetString("speckle_type"));
    Assert.Equal("Walls", wall.GetString("type"));
    Assert.Equal(["Level 1", "Walls"], wall.CollectionPath);
    Assert.Equal("Category", wall.Collection!.Subtype);
    Assert.Equal("Level 1", wall.Collection.Parent!.Name);
    var tier = Assert.Single(model.DefaultSceneView); // added by Build() when none declared
    Assert.Equal(RelKind.InCollection, tier.Relation);

    var g = Assert.Single(wall.Geometries);
    Assert.Equal(9, g.DecodeMesh()!.Value.Vertices.Length);
    Assert.Empty(model.ObjectByApplicationId("door-1")!.Geometries);
  }

  [Fact]
  public async Task Relations_And_Appearance_RoundTrip()
  {
    using var model = await BuildAndRead(b =>
    {
      var layer = b.GetOrAddCollection(["Layer 1"], "Layer");
      var concrete = b.GetOrAddMaterial("mat-1", "Concrete", unchecked((int)0xFF808080), roughness: 0.8);
      var red = b.GetOrAddColor(unchecked((int)0xFFFF0000));
      var l1 = b.GetOrAddLevel("L1", "Level 1", 3.0);
      layer.Color = red; // node plane

      var wall = b.GetOrAddObject("wall-1", layer, null, name: "Wall");
      wall.AddGeometry(Tri()).Material = concrete; // geometry plane
      wall.Level = l1;
      var door = b.GetOrAddObject("door-1", layer, null, name: "Door");
      door.Parent = wall;
      door.Host = wall;
      door.Color = red; // object plane
      door.Level = l1;
      var room = b.GetOrAddObject("room-1", layer, null, name: "Office");
      wall.Bounds(room);
      door.Room = room;
      var a = b.GetOrAddObject("pipe-a", layer, null);
      var c = b.GetOrAddObject("pipe-b", layer, null);
      a.ConnectTo(c);
      var group = b.GetOrAddContainer("grp-1", "Group A", null, "Group");
      wall.AddToGroup(group);
      Assert.Same(concrete, b.GetOrAddMaterial("mat-1", "Concrete", unchecked((int)0xFF808080), roughness: 0.8)); // interned on key
      Assert.Throws<InvalidOperationException>(() => b.GetOrAddMaterial("mat-1", "other name", 0)); // key collision
    });

    var wall = model.ObjectByApplicationId("wall-1")!;
    var door = model.ObjectByApplicationId("door-1")!;
    var room = model.ObjectByApplicationId("room-1")!;

    Assert.Equal("Concrete", wall.Geometries[0].Material!.Name);
    Assert.Equal(0.8, wall.Geometries[0].Material!.Roughness);
    Assert.Equal("Level 1", wall.Level!.Name);
    Assert.Equal(3.0, wall.Level.Elevation);
    Assert.Same(wall, door.Parent);
    Assert.Same(wall, door.Host);
    Assert.Equal([door], wall.Children);
    Assert.Equal(unchecked((int)0xFFFF0000), door.Color!.Argb);
    Assert.Equal(unchecked((int)0xFFFF0000), wall.Collection!.Color!.Argb);
    Assert.Equal([room], wall.BoundsRooms);
    Assert.Same(room, door.Room);
    Assert.Equal([door], room.Contains);
    Assert.Equal(["pipe-b"], model.ObjectByApplicationId("pipe-a")!.ConnectedTo.Select(o => o.ApplicationId));
    Assert.Equal("Group A", Assert.Single(wall.Groups).Name);
    Assert.Single(model.Materials);
    Assert.Single(model.Colors);
    Assert.Single(model.Levels);
  }

  [Fact]
  public async Task Definitions_Placements_Members_RoundTrip()
  {
    using var model = await BuildAndRead(b =>
    {
      var layer = b.GetOrAddCollection(["Blocks"], "Layer");
      var chairDef = b.GetOrAddDefinition(
        "def-chair",
        "Chair",
        d => d.AddGeometry(Tri()).Material = b.GetOrAddMaterial("m", "Fabric", 0)
      );
      double[] t = [1, 0, 0, 10, 0, 1, 0, 20, 0, 0, 1, 0, 0, 0, 0, 1];
      var chair1 = b.GetOrAddObject("chair-1", layer, null, name: "Chair 1");
      chair1.Place(chairDef, t);
      var chair2 = b.GetOrAddObject("chair-2", layer, null, name: "Chair 2");
      chair2.Place(chairDef, t);
      Assert.Same(chairDef, b.GetOrAddDefinition("def-chair", null)); // a placement knows only the id
      Assert.Throws<InvalidOperationException>(() => b.GetOrAddDefinition("def-chair", "x")); // key collision

      // a Rhino-style member object with its own properties, joined to the definition geometry by ordinal
      var tableDef = b.GetOrAddDefinition("def-table", "Table");
      int ord = tableDef.NextMemberOrdinal();
      tableDef.AddGeometry(Tri(5), memberOrd: ord);
      var top = b.GetOrAddObject(
        "table-top",
        layer,
        new Dictionary<string, object?> { ["material"] = "oak" },
        name: "Top"
      );
      tableDef.AddMember(top, ord);
      b.GetOrAddObject("table-1", layer, null, name: "Table 1").Place(tableDef, t);
    });

    var chair1 = model.ObjectByApplicationId("chair-1")!;
    var placement = Assert.Single(chair1.Placements);
    Assert.Equal(10, placement.Transform![3]);
    Assert.Equal("Chair", chair1.Definition!.Name);
    Assert.Equal(2, chair1.Definition.Placements.Count);
    Assert.Equal(["chair-1", "chair-2"], chair1.Definition.Objects.Select(o => o.ApplicationId));
    var g = Assert.Single(chair1.Geometries);
    Assert.Equal("Fabric", g.Material!.Name);
    Assert.NotNull(g.Transform);

    var table = model.ObjectByApplicationId("table-1")!;
    var top = Assert.Single(table.Definition!.Members);
    Assert.Equal("oak", top.GetString("material"));
    Assert.Equal(2, model.Definitions.Count);
  }

  [Fact]
  public async Task ModelExtras_SceneView_RoundTrip()
  {
    using var model = await BuildAndRead(b =>
    {
      var host = b.GetOrAddContainer("model-main", "Main.rvt", null, "Model");
      var l1 = b.GetOrAddLevel("L1", "Level 1", 0);
      var o = b.GetOrAddObject(
        "w",
        null,
        null,
        name: "W",
        rootScalars: [new("category", "Walls"), new("family", "Basic")]
      );
      o.Model = host;
      o.Level = l1;
      b.SceneView(
        "Default",
        isDefault: true,
        SceneViewKey.Rel(RelKind.InModel),
        SceneViewKey.Rel(RelKind.OnLevel),
        SceneViewKey.Eav("category"),
        SceneViewKey.Eav("family")
      );
      b.ModelProperty("modelPlacement.units", "m");
      b.ModelProperty("projectInformation.number", 42.0);
      b.StructuralResult(o, "Base", "reaction", "DL", "Fz", value: 12.5);
      b.CameraView(new CameraView(0, "Front", true, 0, 0, -10, 5, 0, 1, 0, 0, 0, 1));
    });

    var w = model.ObjectByApplicationId("w")!;
    Assert.Equal(4, model.DefaultSceneView.Count);
    Assert.Equal(["Main.rvt", "Level 1", "Walls", "Basic"], w.SceneViewSegments.Select(s => s.Name));
    Assert.IsType<ModelContainer>(w.SceneViewSegments[0].Node);
    Assert.IsType<ModelLevel>(w.SceneViewSegments[1].Node);
    Assert.Null(w.SceneViewSegments[2].Node);
    Assert.Equal("m", model.Properties["modelPlacement.units"]);
    Assert.Equal(42.0, model.Properties["projectInformation.number"]);
    Assert.Equal("Front", Assert.Single(model.CameraViews).Name);
    Assert.Contains(model.Files, f => f.EndsWith(".eav.structural_results.parquet", StringComparison.Ordinal));
  }

  [Fact]
  public void Build_Twice_Throws_And_RenameTo_RekeysFiles()
  {
    using var b = new BundleBuilder(s_app, "m", _dir);
    b.GetOrAddObject("o", null, null);
    var files = b.Build();
    Assert.Throws<InvalidOperationException>(() => b.Build());

    var renamed = files.RenameTo("08de6a66ec");
    Assert.All(renamed.Files, f => Assert.StartsWith("08de6a66ec.", Path.GetFileName(f), StringComparison.Ordinal));
    Assert.All(renamed.Files, f => Assert.True(File.Exists(f)));
    Assert.Equal(files.Files.Count, renamed.ByName.Count);
    Assert.Equal(1, renamed.ObjectCount);
  }

  [Fact]
  public void Relation_CannotBeRetracted()
  {
    using var b = new BundleBuilder(s_app, "m", _dir);
    var a = b.GetOrAddCollection(["A"]);
    var c = b.GetOrAddCollection(["C"]);
    var o = b.GetOrAddObject("o", a, null);
    Assert.Throws<InvalidOperationException>(() => o.Collection = c);
    o.Collection = a; // idempotent
  }

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, true);
    }
  }
}
