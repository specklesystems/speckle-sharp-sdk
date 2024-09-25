using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

[TestFixture]
public sealed class SendReceiveLocal : IDisposable
{
  private IOperations _operations;

  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
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
    (_objId01, var references) = await _operations.Send(myObject, localTransport, false);

    Assert.That(_objId01, Is.Not.Null);
    Assert.That(references, Has.Count.EqualTo(NUM_OBJECTS));

    TestContext.Out.WriteLine($"Written {NUM_OBJECTS + 1} objects. Commit id is {_objId01}");
  }

  [Test(Description = "Pulling a commit locally"), Order(2)]
  public async Task LocalDownload()
  {
    var commitPulled = await _operations.Receive(_objId01.NotNull());

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

    (_objId01, _) = await _operations.Send(myObject, _sut, false);

    var commitPulled = await _operations.Receive(_objId01);
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

    (_objId01, _) = await _operations.Send(myObject, _sut, false);

    Assert.That(_objId01, Is.Not.Null);
    TestContext.Out.WriteLine($"Written {NUM_OBJECTS + 1} objects. Commit id is {_objId01}");

    var objsPulled = await _operations.Receive(_objId01);
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

    (_objId01, _) = await _operations.Send(myObject, _sut, false);

    Assert.That(_objId01, Is.Not.Null);

    var objsPulled = await _operations.Receive(_objId01);
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

    (_objId01, _) = await _operations.Send(obj, _sut, false);

    Assert.That(_objId01, Is.Not.Null);
    TestContext.Out.WriteLine($"Written {NUM_OBJECTS + 1} objects. Commit id is {_objId01}");

    var objPulled = await _operations.Receive(_objId01);

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

    ConcurrentBag<ProgressArgs>? progress = null;
    (_commitId02, _) = await _operations.Send(
      myObject,
      _sut,
      false,
      onProgressAction: dict =>
      {
        progress = dict;
      }
    );
    progress.NotNull();
    Assert.That(progress, Has.Count.GreaterThanOrEqualTo(1));
  }

  [Test(Description = "Should show progress!"), Order(5)]
  public async Task DownloadProgressReports()
  {
    ConcurrentBag<ProgressArgs>? progress = null;
    await _operations.Receive(
      _commitId02.NotNull(),
      onProgressAction: dict =>
      {
        progress = dict;
      }
    );
    progress.NotNull();
    Assert.That(progress, Has.Count.GreaterThanOrEqualTo(1));
  }

  [Test(Description = "Should not dispose of transports if so specified.")]
  public async Task ShouldNotDisposeTransports()
  {
    var @base = new Base();
    @base["test"] = "the best";

    SQLiteTransport myLocalTransport = new();
    var sendResult = await _operations.Send(@base, myLocalTransport, false);
    await _operations.Send(@base, myLocalTransport, false);

    _ = await _operations.Receive(sendResult.rootObjId, null, myLocalTransport);
    await _operations.Receive(sendResult.rootObjId, null, myLocalTransport);
  }

  public void Dispose()
  {
    _sut.Dispose();
  }
}
