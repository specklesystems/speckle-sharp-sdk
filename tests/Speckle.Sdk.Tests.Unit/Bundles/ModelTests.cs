using Microsoft.Extensions.Logging.Abstractions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Speckle.Sdk.Pipelines.Send.Artifacts;
using SpecKind = Speckle.Bundle.Spec.NodeKind;

namespace Speckle.Sdk.Tests.Unit.Bundles;

/// <summary>The Receive3 surface over a synthetic bundle written with <see cref="ObjectsArtifactPipeline"/>.</summary>
public sealed class ModelTests : IDisposable
{
  private static readonly SpeckleApplication s_producer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.0",
    Slug = "test",
    SpeckleVersion = "0.0.0",
  };

  private readonly string _dir = Path.Combine(Path.GetTempPath(), "SpeckleModelTests", Guid.NewGuid().ToString("N"));

  private async Task<Model> BuildModel(bool geometryDownloaded = true)
  {
    using (var pipeline = new ObjectsArtifactPipeline(_dir, "m", s_producer))
    {
      int level = pipeline.AddCollection("lvl-1", "Level 1", null, "Level");
      int walls = pipeline.AddCollection("walls", "Walls", level, "Category");

      int wall = pipeline.InternObject("wall-1");
      pipeline.AddProperties(
        "wall-1",
        new Dictionary<string, object?>
        {
          ["Constraints"] = new Dictionary<string, object?> { ["Base Offset"] = 0.5, ["Top Offset"] = 0.0 },
          ["Identity Data"] = new Dictionary<string, object?> { ["Mark"] = "W-01" },
        },
        [new("name", "Basic Wall"), new("units", "m")]
      );
      int g = pipeline.AddGeometry(
        "wall-1:g0",
        new Mesh
        {
          vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0],
          faces = [3, 0, 1, 2],
          units = "m",
        }
      );
      pipeline.Display(wall, g, 0);
      pipeline.InCollection(wall, walls, 0);

      int door = pipeline.InternObject("door-1");
      pipeline.AddProperties("door-1", new Dictionary<string, object?> { ["Width"] = 0.9 }, [new("name", "Door")]);
      pipeline.InCollection(door, walls, 0);

      // A placed object: chair-1 renders a block definition through an INSTANCE node carrying a translation.
      int chairDef = pipeline.AddDefinition("def-chair", "Chair");
      int chairGeom = pipeline.AddGeometry(
        "def-chair:g0",
        new Mesh
        {
          vertices = [0, 0, 0, 0.5, 0, 0, 0, 0.5, 0, 0, 0, 0.5],
          faces = [3, 0, 1, 2, 3, 0, 1, 3],
          units = "m",
        }
      );
      pipeline.Defines(chairDef, chairGeom, 0);
      double[] translate = [1, 0, 0, 10, 0, 1, 0, 20, 0, 0, 1, 0, 0, 0, 0, 1];
      int chairInstance = pipeline.AddInstance("chair-1:placement", chairDef, translate, "m");
      int chair = pipeline.InternObject("chair-1");
      pipeline.AddProperties("chair-1", new Dictionary<string, object?>(), [new("name", "Chair 1")]);
      pipeline.DisplayInstance(chair, chairInstance, 0);
      pipeline.InCollection(chair, walls, 0);

      // Relationships: level, ownership, hosting, room bounds, connectivity, appearance on three planes.
      int level1 = pipeline.AddLevel("L1", "Level 1", 0.0);
      pipeline.OnLevel(wall, level1);
      pipeline.OnLevel(door, level1);
      pipeline.Subelement(wall, door, 0); // door is a component of the wall
      pipeline.HostedOn(door, wall); // ...and placed on it
      int room = pipeline.InternObject("room-1");
      pipeline.AddProperties("room-1", new Dictionary<string, object?> { ["Area"] = 12.0 }, [new("name", "Office")]);
      pipeline.Bounds(wall, room, 0);
      pipeline.InRoom(door, room, 0);
      int pipeA = pipeline.InternObject("pipe-a");
      int pipeB = pipeline.InternObject("pipe-b");
      pipeline.AddProperties("pipe-a", new Dictionary<string, object?>(), [new("name", "Pipe A")]);
      pipeline.AddProperties("pipe-b", new Dictionary<string, object?>(), [new("name", "Pipe B")]);
      pipeline.ConnectsTo(pipeA, pipeB);
      int concrete = pipeline.AddMaterial("mat-concrete", "Concrete", unchecked((int)0xFF808080), 1.0, 0.0, 0.8);
      pipeline.HasMaterial(g, concrete); // geometry plane
      int red = pipeline.AddColor(unchecked((int)0xFFFF0000));
      pipeline.ObjectHasColor(door, red); // object plane
      pipeline.NodeHasColor(walls, red); // node plane (the container)

      // Default scene view = the authored IN_COLLECTION tree (so CollectionPath == container ancestry).
      pipeline.AddSceneView(new SceneView(0, "Default", true, [SceneViewKey.Rel(RelKind.InCollection)]));

      pipeline.SetProducer(s_producer);
      pipeline.Complete();
    }

    var bundle = await ArtefactBundleReader.ReadAsync(_dir, ArtefactReadOptions.Columnar, CancellationToken.None);
    var files = Directory.GetFiles(_dir);
    return new Model("p", "m", "v", _dir, files, bundle, geometryDownloaded, NullLogger.Instance);
  }

  [Fact]
  public async Task Objects_ExposeFlatPathKeyedProperties()
  {
    using var model = await BuildModel();

    Assert.Equal("m", model.Units);
    Assert.Equal(6, model.Objects.Count);

    var wall = model.Objects.Single(o => o.ApplicationId == "wall-1");
    Assert.Equal("Basic Wall", wall.Name);
    Assert.Equal(0.5, wall["Constraints.Base Offset"]);
    Assert.Equal("W-01", wall.Properties["Identity Data.Mark"]);
    Assert.Null(wall["does.not.exist"]);
    Assert.Empty(wall.TypeProperties);
  }

  [Fact]
  public async Task Objects_AreLinqQueryable()
  {
    using var model = await BuildModel();

    var wide = model.Objects.Where(o => o["Width"] is double w && w > 0.5).Select(o => o.ApplicationId).ToList();
    Assert.Equal(["door-1"], wide);
  }

  [Fact]
  public async Task Objects_CarryCollectionPath()
  {
    using var model = await BuildModel();

    var wall = model.Objects.Single(o => o.ApplicationId == "wall-1");
    Assert.Equal(["Level 1", "Walls"], wall.CollectionPath);
  }

  [Fact]
  public async Task Dispose_DeletesBundleFiles_ButDataStaysUsable()
  {
    var model = await BuildModel();
    Assert.True(Directory.Exists(model.Directory));
    Assert.NotEmpty(model.Files);

    model.Dispose();

    Assert.False(Directory.Exists(model.Directory));
    Assert.Equal(6, model.Objects.Count);
    Assert.Equal(6, model.Bundle.ObjectAppIds.Count);
  }

  [Fact]
  public async Task Geometries_LoadLazily_FromDisk()
  {
    using var model = await BuildModel();

    Assert.False(model.IsGeometryLoaded);
    Assert.Empty(model.Bundle.Geometries); // deferred: the neutral parse never opened the shards

    var geometries = model.Geometries;

    Assert.True(model.IsGeometryLoaded);
    Assert.Equal(2, geometries.Count); // wall mesh + chair definition mesh
    Assert.All(geometries.Values, g => Assert.True(g.IsSgeo));
  }

  [Fact]
  public async Task ObjectGeometries_Direct_DecodeToMesh()
  {
    using var model = await BuildModel();
    var wall = model.Objects.Single(o => o.ApplicationId == "wall-1");

    var g = Assert.Single(wall.Geometries);
    Assert.Equal(GeometryRole.Display, g.Role);
    Assert.Null(g.Transform);
    var mesh = g.DecodeMesh();
    Assert.NotNull(mesh);
    Assert.Equal(9, mesh.Value.Vertices.Length); // 3 verts × xyz
    Assert.Equal("m", mesh.Value.Units);

    var door = model.Objects.Single(o => o.ApplicationId == "door-1");
    Assert.Empty(door.Geometries); // property-only object
  }

  [Fact]
  public async Task ObjectGeometries_Placed_CarryDefinitionGeometryAndTransform()
  {
    using var model = await BuildModel();
    var chair = model.Objects.Single(o => o.ApplicationId == "chair-1");

    var g = Assert.Single(chair.Geometries);
    Assert.Equal(GeometryRole.Display, g.Role);
    Assert.NotNull(g.Transform);
    Assert.Equal(10, g.Transform![3]); // x translation from the INSTANCE node (row-major, as stored)
    Assert.Equal(20, g.Transform[7]);
    Assert.Equal(12, g.DecodeMesh()!.Value.Vertices.Length); // the shared definition mesh, untouched
  }

  [Fact]
  public async Task Geometries_NotDownloaded_Throws()
  {
    using var model = await BuildModel(geometryDownloaded: false);

    var ex = Assert.Throws<InvalidOperationException>(() => model.Geometries);
    Assert.Contains("IncludeGeometry", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Geometries_AfterDispose_Throws()
  {
    var model = await BuildModel();
    model.Dispose();

    var ex = Assert.Throws<InvalidOperationException>(() => model.Geometries);
    Assert.Contains("disposed", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Relationships_ObjectToObject()
  {
    using var model = await BuildModel();
    var wall = model.ObjectByApplicationId("wall-1")!;
    var door = model.ObjectByApplicationId("door-1")!;
    var room = model.ObjectByApplicationId("room-1")!;

    Assert.Same(wall, door.Parent);
    Assert.Equal([door], wall.Children);
    Assert.Same(wall, door.Host);
    Assert.Equal([door], wall.Hosted);
    Assert.Null(wall.Parent);
    Assert.Equal([room], wall.BoundsRooms);
    Assert.Equal([wall], room.BoundedBy);
    Assert.Same(room, door.Room); // rooms are objects, not nodes
    Assert.Equal([door], room.Contains);
    Assert.Null(wall.Room);

    var a = model.ObjectByApplicationId("pipe-a")!;
    var b = model.ObjectByApplicationId("pipe-b")!;
    Assert.Equal([b], a.ConnectedTo);
    Assert.Equal([a], b.ConnectedTo); // undirected view
  }

  [Fact]
  public async Task Relationships_ObjectToNode()
  {
    using var model = await BuildModel();
    var wall = model.ObjectByApplicationId("wall-1")!;
    var door = model.ObjectByApplicationId("door-1")!;

    Assert.Equal("Level 1", wall.Level!.Name);
    Assert.Equal(SpecKind.LEVEL, wall.Level.Kind);
    Assert.Equal(0.0, wall.Level.Elevation);
    Assert.Equal(2, wall.Level.Objects.Count); // wall + door
    Assert.Equal("Level 1", door.Host!.Level!.Name); // chaining

    Assert.Equal("Walls", wall.Collection!.Name);
    Assert.Equal("Category", wall.Collection.Subtype);
    Assert.Equal("Level", wall.Collection.Parent!.Subtype);
    Assert.Equal("Level 1", wall.Collection.Parent!.Name);
    Assert.Equal(["Level 1", "Walls"], wall.Collection.Path);
    Assert.Contains(wall, wall.Collection.Objects);
    Assert.Equal([wall.Collection], wall.Collection.Parent.Children);

    var single = Assert.Single(model.Levels);
    Assert.Same(wall.Level, single);
    Assert.Equal(2, model.Collections.Count);
  }

  [Fact]
  public async Task Relationships_Appearance_ThreePlanes()
  {
    using var model = await BuildModel();
    var wall = model.ObjectByApplicationId("wall-1")!;
    var door = model.ObjectByApplicationId("door-1")!;

    // geometry plane
    var wallGeometry = Assert.Single(wall.Geometries);
    Assert.Equal("Concrete", wallGeometry.Material!.Name);
    Assert.Equal(0.8, wallGeometry.Material.Roughness);
    Assert.Null(wallGeometry.Color);
    Assert.Null(wall.Material); // nothing on the object plane

    // object plane
    Assert.Equal(unchecked((int)0xFFFF0000), door.Color!.Argb);
    Assert.Equal(SpecKind.COLOR, door.Color.Kind);
    Assert.IsType<ModelColor>(door.Color);

    // node plane
    Assert.Equal(unchecked((int)0xFFFF0000), wall.Collection!.Color!.Argb);

    Assert.Single(model.Materials);
    Assert.Single(model.Colors);
  }

  [Fact]
  public async Task Relationships_Instancing()
  {
    using var model = await BuildModel();
    var chair = model.ObjectByApplicationId("chair-1")!;

    ModelInstance placement = Assert.Single(chair.Placements);
    Assert.Equal(SpecKind.INSTANCE, placement.Kind);
    Assert.Same(chair.Definition, placement.Definition);
    Assert.Equal(10, placement.Transform![3]);
    Assert.Equal("Chair", chair.Definition!.Name);
    Assert.Equal([placement], chair.Definition.Placements);
    Assert.Same(placement, Assert.Single(chair.Geometries).Placement);

    var definition = Assert.Single(model.Definitions);
    Assert.Same(chair.Definition, definition);
    Assert.Null(model.ObjectByApplicationId("wall-1")!.Definition);
  }

  [Fact]
  public async Task SceneView_TiersAndSegments()
  {
    using var model = await BuildModel();

    var tier = Assert.Single(model.DefaultSceneView);
    Assert.True(tier.IsRelation);
    Assert.Equal(RelKind.InCollection, tier.Relation);

    var wall = model.ObjectByApplicationId("wall-1")!;
    var segments = wall.SceneViewSegments;
    Assert.Equal(["Level 1", "Walls"], segments.Select(s => s.Name));
    Assert.All(segments, s => Assert.IsType<ModelContainer>(s.Node));
    Assert.Same(wall.Collection, segments[^1].Node);
    Assert.Empty(model.UnknownRelations);
  }

  [Fact]
  public async Task Appearance_EffectiveChains()
  {
    using var model = await BuildModel();
    var wall = model.ObjectByApplicationId("wall-1")!;
    var chair = model.ObjectByApplicationId("chair-1")!;

    var wallGeometry = Assert.Single(wall.Geometries);
    Assert.Same(wall, wallGeometry.Owner);
    Assert.Equal("Concrete", wallGeometry.EffectiveMaterial!.Name); // geometry plane wins for material
    Assert.Same(wall.Collection!.Color, wallGeometry.EffectiveColor); // no geometry/object colour → container's

    var chairGeometry = Assert.Single(chair.Geometries);
    Assert.Null(chairGeometry.Material);
    Assert.Null(chairGeometry.EffectiveMaterial); // nothing anywhere in the chain
  }

  [Fact]
  public async Task Definitions_ReverseLookups()
  {
    using var model = await BuildModel();
    var chair = model.ObjectByApplicationId("chair-1")!;
    var definition = Assert.Single(chair.Definitions);

    Assert.Same(definition, chair.Definition);
    Assert.Equal([chair], definition.Objects);
    Assert.Empty(definition.Members); // Revit-shaped: no DEFINES_MEMBER
  }

  [Fact]
  public async Task Properties_AreColumnarViews()
  {
    using var model = await BuildModel();
    var wall = model.ObjectByApplicationId("wall-1")!;

    Assert.Empty(model.Bundle.Properties); // nothing nested was built
    Assert.NotNull(model.Bundle.PropertyTable);
    Assert.Equal(0.5, wall.GetDouble("Constraints.Base Offset"));
    Assert.Equal("W-01", wall.GetString("Identity Data.Mark"));
    Assert.Null(wall.GetDouble("Identity Data.Mark")); // typed miss
    Assert.Equal("m", wall.GetString("units")); // root scalar via fallback
    Assert.Equal(3, wall.Properties.Count);
    Assert.Contains("Constraints.Base Offset", model.PropertyPaths);
    Assert.DoesNotContain("name", model.PropertyPaths);

    Assert.Equal(["door-1"], model.ObjectsWith("Width").Select(o => o.ApplicationId));
    Assert.Equal(["wall-1"], model.ObjectsWith("Constraints.Top Offset").Select(o => o.ApplicationId));
    Assert.Empty(model.ObjectsWith("nope"));
  }

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, true);
    }
  }
}
