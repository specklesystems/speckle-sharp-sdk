using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Pipelines.Receive.Artifacts;
using Model = Speckle.Sdk.Bundles.Model;

namespace Speckle.Sdk.Tests.Integration.Bundles;

/// <summary>
/// The Speckle 2026.9.0 round trip against a real server: BundleBuilder → Send3 (ingestion, sign/PUT/complete) →
/// Receive3 (artifacts listing, download, parse) → the same objects, properties, relations and geometry come back;
/// and the version's <c>referencedObject</c> is the bundle reference Receive2 dispatches on.
/// </summary>
[Trait("Server", "Internal")]
public sealed class SendReceiveBundleTests : IAsyncLifetime
{
  private static readonly SpeckleApplication s_app = new()
  {
    HostApplication = "IntegrationTest",
    HostApplicationVersion = "1.0",
    Slug = "test",
    SpeckleVersion = "0.0.0",
  };

  private IClient _client = null!;
  private string _projectId = null!;
  private string _modelId = null!;

  public async Task InitializeAsync()
  {
    _client = await Fixtures.SeedUserWithClient();
    var project = await _client.Project.Create(new("Bundle round trip", null, ProjectVisibility.Private));
    _projectId = project.id;
    var model = await _client.Model.Create(new("bundle", null, _projectId));
    _modelId = model.id;
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Send3_Then_Receive3_RoundTrips()
  {
    var operations = Fixtures.ServiceProvider.GetRequiredService<IOperations>();
    var account = _client.Account;

    // ── send ────────────────────────────────────────────────────────────────────────────────────────────
    SendResult sent;
    using (var b = new BundleBuilder(s_app, "m"))
    {
      var walls = b.GetOrAddContainerPath(["Level 1", "Walls"], subtype: "Category");
      var concrete = b.GetOrAddMaterial("mat-concrete", "Concrete", unchecked((int)0xFF808080), roughness: 0.8);
      var l1 = b.GetOrAddLevel("L1", "Level 1", 0);
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
      wall.AddGeometry(
        new Mesh
        {
          vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0],
          faces = [3, 0, 1, 2],
          units = "m",
        }
      ).Material = concrete;
      wall.Level = l1;
      var door = b.GetOrAddObject("door-1", walls, new Dictionary<string, object?> { ["Width"] = 0.9 }, name: "Door");
      door.Host = wall;
      door.Parent = wall;
      b.ModelProperty("projectInformation.number", 42.0);

      sent = await operations.Send3(account, _projectId, _modelId, b, null, CancellationToken.None);
    }

    Assert.Equal(_projectId, sent.ProjectId);
    Assert.Equal(_modelId, sent.ModelId);
    Assert.Equal(2, sent.ObjectCount);
    Assert.False(string.IsNullOrEmpty(sent.VersionId));

    // ── the version exists once the ingestion completes; poll briefly ──────────────────────────────────
    Speckle.Sdk.Api.GraphQL.Models.Version? version = null;
    for (int i = 0; i < 60 && version is null; i++)
    {
      try
      {
        version = await _client.Version.Get(sent.VersionId, _projectId);
      }
      catch (SpeckleException)
      {
        await Task.Delay(500);
      }
    }
    Assert.NotNull(version);
    Assert.Equal(sent.BundleReference, version.referencedObject); // what Receive2 dispatches on
    Assert.True(BundleReference.TryParse(version.referencedObject, out _));

    // ── receive ─────────────────────────────────────────────────────────────────────────────────────────
    using Model received = await operations.Receive3(
      account,
      _projectId,
      _modelId,
      sent.VersionId,
      null,
      CancellationToken.None
    );

    Assert.Equal("m", received.Units);
    Assert.Equal(2, received.Objects.Count);
    var wallBack = received.ObjectByApplicationId("wall-1")!;
    Assert.Equal("Basic Wall", wallBack.Name);
    Assert.Equal(0.5, wallBack.GetDouble("Constraints.Base Offset"));
    Assert.Equal("W-01", wallBack.GetString("Identity Data.Mark"));
    Assert.Equal(["Level 1", "Walls"], wallBack.CollectionPath);
    Assert.Equal("Level 1", wallBack.Level!.Name);
    var g = Assert.Single(wallBack.Geometries);
    Assert.Equal(9, g.DecodeMesh()!.Value.Vertices.Length);
    Assert.Equal("Concrete", g.Material!.Name);
    var doorBack = received.ObjectByApplicationId("door-1")!;
    Assert.Same(wallBack, doorBack.Host);
    Assert.Same(wallBack, doorBack.Parent);
    Assert.Equal(42.0, received.Properties["projectInformation.number"]);

    // ── and by url, latest version ──────────────────────────────────────────────────────────────────────
    using Model latest = await operations.Receive3(
      account,
      new Uri($"{account.serverInfo.url.TrimEnd('/')}/projects/{_projectId}/models/{_modelId}"),
      null,
      CancellationToken.None
    );
    Assert.Equal(sent.VersionId, latest.VersionId);
  }
}
