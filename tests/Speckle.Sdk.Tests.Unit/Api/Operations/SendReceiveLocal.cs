using System.Collections.Concurrent;
using NUnit.Framework;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

[TestFixture]
public sealed class SendReceiveLocal : IDisposable
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
  }

  private string? _objId01;
  private string? _commitId02;

  private const int NUM_OBJECTS = 3001;

  private readonly SQLiteTransport _sut = new();

  [Test(Description = "Pushing a commit locally"), Order(1)]
  public async Task LocalUpload()
  {
    var myObject = new Base();
    var rand = new Random();

    myObject["@items"] = new List<Base>();

    for (int i = 0; i < NUM_OBJECTS; i++)
    {
      ((List<Base>)myObject["@items"].NotNull()).Add(
        new Point(i, i, i + rand.NextDouble()) { applicationId = i + "-___/---" }
      );
    }

    using SQLiteTransport localTransport = new();
    _objId01 = await Sdk.Api.Operations.Send(myObject, localTransport, false);

    Assert.That(_objId01, Is.Not.Null);
    TestContext.Out.WriteLine($"Written {NUM_OBJECTS + 1} objects. Commit id is {_objId01}");
  }

  [Test(Description = "Pulling a commit locally"), Order(2)]
  public async Task LocalDownload()
  {
    var commitPulled = await Sdk.Api.Operations.Receive(_objId01!);

    Assert.That(((List<object>)commitPulled["@items"].NotNull())[0], Is.TypeOf<Point>());
    Assert.That(((List<object>)commitPulled["@items"].NotNull()), Has.Count.EqualTo(NUM_OBJECTS));
  }

  [Test(Description = "Pushing and Pulling a commit locally")]
  public async Task LocalUploadDownload()
  {
    var myObject = new Base();
    myObject["@items"] = new List<Base>();

    var rand = new Random();

    for (int i = 0; i < NUM_OBJECTS; i++)
    {
      ((List<Base>)myObject["@items"].NotNull()).Add(
        new Point(i, i, i + rand.NextDouble()) { applicationId = i + "-___/---" }
      );
    }

    _objId01 = await Sdk.Api.Operations.Send(myObject, _sut, false);

    var commitPulled = await Sdk.Api.Operations.Receive(_objId01);
    List<object> items = (List<object>)commitPulled["@items"].NotNull();

    Assert.That(items, Has.All.TypeOf<Point>());
    Assert.That(items, Has.Count.EqualTo(NUM_OBJECTS));
  }

  [Test(Description = "Pushing and pulling a commit locally"), Order(3)]
  public async Task LocalUploadDownloadSmall()
  {
    var myObject = new Base();
    myObject["@items"] = new List<Base>();

    var rand = new Random();

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)myObject["@items"].NotNull()).Add(
        new Point(i, i, i + rand.NextDouble()) { applicationId = i + "-ugh/---" }
      );
    }

    _objId01 = await Sdk.Api.Operations.Send(myObject, _sut, false);

    Assert.That(_objId01, Is.Not.Null);
    TestContext.Out.WriteLine($"Written {NUM_OBJECTS + 1} objects. Commit id is {_objId01}");

    var objsPulled = await Sdk.Api.Operations.Receive(_objId01);
    Assert.That(((List<object>)objsPulled["@items"].NotNull()), Has.Count.EqualTo(30));
  }

  [Test(Description = "Pushing and pulling a commit locally"), Order(3)]
  public async Task LocalUploadDownloadListDic()
  {
    var myList = new List<object> { 1, 2, 3, "ciao" };
    var myDic = new Dictionary<string, object>
    {
      { "a", myList },
      { "b", 2 },
      { "c", "ciao" }
    };

    var myObject = new Base();
    myObject["@dictionary"] = myDic;
    myObject["@list"] = myList;

    _objId01 = await Sdk.Api.Operations.Send(myObject, _sut, false);

    Assert.That(_objId01, Is.Not.Null);

    var objsPulled = await Sdk.Api.Operations.Receive(_objId01);
    Assert.That(
      ((List<object>)((Dictionary<string, object>)objsPulled["@dictionary"].NotNull())["a"]).First(),
      Is.EqualTo(1)
    );
    Assert.That(((List<object>)objsPulled["@list"].NotNull()).Last(), Is.EqualTo("ciao"));
  }

  [Test(Description = "Pushing and pulling a random object, with our without detachment"), Order(3)]
  public async Task UploadDownloadNonCommitObject()
  {
    var obj = new Base();
    // Here we are creating a "non-standard" object to act as a base for our multiple objects.
    ((dynamic)obj).LayerA = new List<Base>(); // Layer a and b will be stored "in" the parent object,
    ((dynamic)obj).LayerB = new List<Base>();
    ((dynamic)obj)["@LayerC"] = new List<Base>(); // whereas this "layer" will be stored as references only.
    ((dynamic)obj)["@LayerD"] = new Point[] { new(), new(12, 3, 4) };
    var rand = new Random();

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)((dynamic)obj).LayerA).Add(new Point(i, i, i + rand.NextDouble()) { applicationId = i + "foo" });
    }

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)((dynamic)obj).LayerB).Add(new Point(i, i, i + rand.NextDouble()) { applicationId = i + "bar" });
    }

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)((dynamic)obj)["@LayerC"]).Add(new Point(i, i, i + rand.NextDouble()) { applicationId = i + "baz" });
    }

    _objId01 = await Sdk.Api.Operations.Send(obj, _sut, false);

    Assert.That(_objId01, Is.Not.Null);
    TestContext.Out.WriteLine($"Written {NUM_OBJECTS + 1} objects. Commit id is {_objId01}");

    var objPulled = await Sdk.Api.Operations.Receive(_objId01);

    Assert.That(objPulled, Is.TypeOf<Base>());

    // Note: even if the layers were originally declared as lists of "Base" objects, on deserialisation we cannot know that,
    // as it's a dynamic property. Dynamic properties, if their content value is ambigous, will default to a common-sense standard.
    // This specifically manifests in the case of lists and dictionaries: List<AnySpecificType> will become List<object>, and
    // Dictionary<string, MyType> will deserialize to Dictionary<string,object>.
    var layerA = ((dynamic)objPulled)["LayerA"] as List<object>;
    Assert.That(layerA, Has.Count.EqualTo(30));

    var layerC = (List<object>)((dynamic)objPulled)["@LayerC"];
    Assert.That(layerC, Has.Count.EqualTo(30));
    Assert.That(layerC[0], Is.TypeOf<Point>());

    var layerD = ((dynamic)objPulled)["@LayerD"] as List<object>;
    Assert.That(layerD, Has.Count.EqualTo(2));
  }

  [Test(Description = "Should show progress!"), Order(4)]
  public async Task UploadProgressReports()
  {
    Base myObject = new() { ["items"] = new List<Base>() };
    var rand = new Random();

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)myObject["items"].NotNull()).Add(
        new Point(i, i, i + rand.NextDouble()) { applicationId = i + "-fab/---" }
      );
    }

    ConcurrentDictionary<string, int>? progress = null;
    _commitId02 = await Sdk.Api.Operations.Send(
      myObject,
      _sut,
      false,
      onProgressAction: dict =>
      {
        progress = dict;
      }
    );
    progress.NotNull();
    Assert.That(progress.Keys, Has.Count.GreaterThanOrEqualTo(1));
  }

  [Test(Description = "Should show progress!"), Order(5)]
  public async Task DownloadProgressReports()
  {
    ConcurrentDictionary<string, int>? progress = null;
    var pulledCommit = await Sdk.Api.Operations.Receive(
      _commitId02!,
      onProgressAction: dict =>
      {
        progress = dict;
      }
    );
    progress.NotNull();
    Assert.That(progress.Keys, Has.Count.GreaterThanOrEqualTo(1));
  }

  [Test(Description = "Should not dispose of transports if so specified.")]
  public async Task ShouldNotDisposeTransports()
  {
    var @base = new Base();
    @base["test"] = "the best";

    SQLiteTransport myLocalTransport = new();
    var id = await Sdk.Api.Operations.Send(@base, myLocalTransport, false);
    await Sdk.Api.Operations.Send(@base, myLocalTransport, false);

    _ = await Sdk.Api.Operations.Receive(id, null, myLocalTransport);
    await Sdk.Api.Operations.Receive(id, null, myLocalTransport);
  }

  //[Test]
  //public async Task DiskTransportTest()
  //{
  //  var myObject = new Base();
  //  myObject["@items"] = new List<Base>();
  //  myObject["test"] = "random";

  //  var rand = new Random();

  //  for (int i = 0; i < 100; i++)
  //  {
  //    ((List<Base>)myObject["@items"]).Add(new Point(i, i, i) { applicationId = i + "-___/---" });
  //  }

  //  var dt = new Speckle.Sdk.Transports.Speckle.Speckle.Sdk.Transports();
  //  var id = await Operations.Send(myObject, new List<ITransport>() { dt }, false);

  //  Assert.IsNotNull(id);

  //  var rebase = await Operations.Receive(id, dt);

  //  Assert.AreEqual(rebase.GetId(true), id);
  //}

  public void Dispose()
  {
    _sut.Dispose();
  }
}
