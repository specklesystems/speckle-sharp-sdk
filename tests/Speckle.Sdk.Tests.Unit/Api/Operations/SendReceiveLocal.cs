using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public sealed class SendReceiveLocal : IDisposable
{
  private readonly IOperations _operations;

  public SendReceiveLocal()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  private const int NUM_OBJECTS = 3001;

  private readonly SQLiteTransport _sut = new();

  public void Dispose() => _sut.Dispose();

  [Fact(DisplayName = "Pushing a commit locally")]
  public async Task LocalUploadAndDownload()
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
    (var objId01, var references) = await _operations.Send(myObject, localTransport, false);

    objId01.Should().NotBeNull();
    references.Count.Should().Be(NUM_OBJECTS);

    var commitPulled = await _operations.Receive(objId01.NotNull());

    ((List<object>)commitPulled["@items"].NotNull())[0].Should().BeOfType<Point>();
    ((List<object>)commitPulled["@items"].NotNull()).Count.Should().Be(NUM_OBJECTS);
  }

  [Fact(DisplayName = "Pushing and Pulling a commit locally")]
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

    (var objId01, _) = await _operations.Send(myObject, _sut, false);

    var commitPulled = await _operations.Receive(objId01);
    List<object> items = (List<object>)commitPulled["@items"].NotNull();
    items.Should().AllSatisfy(x => x.Should().BeOfType<Point>());
    items.Count.Should().Be(NUM_OBJECTS);
  }

  [Fact(DisplayName = "Pushing and pulling a commit locally")]
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

    (var objId01, _) = await _operations.Send(myObject, _sut, false);

    objId01.Should().NotBeNull();

    var objsPulled = await _operations.Receive(objId01);
    ((List<object>)objsPulled["@items"].NotNull()).Count.Should().Be(30);
  }

  [Fact(DisplayName = "Pushing and pulling a commit locally")]
  public async Task LocalUploadDownloadListDic()
  {
    var myList = new List<object> { 1, 2, 3, "ciao" };
    var myDic = new Dictionary<string, object>
    {
      { "a", myList },
      { "b", 2 },
      { "c", "ciao" },
    };

    var myObject = new Base();
    myObject["@dictionary"] = myDic;
    myObject["@list"] = myList;

    (var _objId01, _) = await _operations.Send(myObject, _sut, false);

    _objId01.Should().NotBeNull();

    var objsPulled = await _operations.Receive(_objId01);
    ((List<object>)((Dictionary<string, object>)objsPulled["@dictionary"].NotNull())["a"]).First().Should().Be(1);
    ((List<object>)objsPulled["@list"].NotNull()).Last().Should().Be("ciao");
  }

  [Fact(DisplayName = "Pushing and pulling a random object, with or without detachment")]
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

    (var objId01, _) = await _operations.Send(obj, _sut, false);

    objId01.Should().NotBeNull();

    var objPulled = await _operations.Receive(objId01);

    objPulled.Should().BeOfType<Base>();

    // Note: even if the layers were originally declared as lists of "Base" objects, on deserialisation we cannot know that,
    // as it's a dynamic property. Dynamic properties, if their content value is ambigous, will default to a common-sense standard.
    // This specifically manifests in the case of lists and dictionaries: List<AnySpecificType> will become List<object>, and
    // Dictionary<string, MyType> will deserialize to Dictionary<string,object>.
    var layerA = ((dynamic)objPulled)["LayerA"] as List<object>;
    layerA?.Count.Should().Be(30);

    var layerC = (List<object>)((dynamic)objPulled)["@LayerC"];
    layerC.Count.Should().Be(30);
    layerC[0].Should().BeOfType<Point>();

    var layerD = ((dynamic)objPulled)["@LayerD"] as List<object>;
    layerD?.Count.Should().Be(2);
  }

  [Fact(DisplayName = "Should show progress!")]
  public async Task UploadAndDownloadProgressReports()
  {
    Base myObject = new() { ["items"] = new List<Base>() };
    var rand = new Random();

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)myObject["items"].NotNull()).Add(
        new Point(i, i, i + rand.NextDouble()) { applicationId = i + "-fab/---" }
      );
    }

    (var commitId02, _) = await _operations.Send(myObject, _sut, false);

    ProgressArgs? progress = null;
    await _operations.Receive(
      commitId02.NotNull(),
      onProgressAction: new UnitTestProgress<ProgressArgs>(x =>
      {
        progress = x;
      })
    );
    progress.Should().NotBeNull();
  }

  [Fact(DisplayName = "Should not dispose of transports if so specified.")]
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
}
