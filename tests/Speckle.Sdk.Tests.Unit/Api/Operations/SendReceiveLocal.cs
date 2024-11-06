using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public sealed class SendReceiveLocal : IDisposable
{
  private IOperations _operations;

  public  SendReceiveLocal()
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

  [Fact]
  public async Task All()
  {
    await LocalUpload();
    await LocalDownload();
    await LocalUploadDownload();
    await LocalUploadDownloadSmall();
    await LocalUploadDownloadListDic();
    await UploadDownloadNonCommitObject();
    await UploadProgressReports();
    await DownloadProgressReports();
  }
  private async Task LocalUpload()
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

    _objId01.ShouldNotBeNull();
    references.Count.ShouldBe(NUM_OBJECTS);
  }

  private async Task LocalDownload()
  {
    var commitPulled = await _operations.Receive(_objId01.NotNull());

    ((List<object>)commitPulled["@items"].NotNull())[0].ShouldBeAssignableTo<Point>();
    ((List<object>)commitPulled["@items"].NotNull()).Count.ShouldBe(NUM_OBJECTS);
  }

  private async Task LocalUploadDownload()
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

    items.ShouldAllBe(x => x.GetType() == typeof(Point));
    items.Count.ShouldBe(NUM_OBJECTS);
  }

  private async Task LocalUploadDownloadSmall()
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

    _objId01.ShouldNotBeNull();

    var objsPulled = await _operations.Receive(_objId01);
    ((List<object>)objsPulled["@items"].NotNull()).Count.ShouldBe(30);
  }

  private async Task LocalUploadDownloadListDic()
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

    (_objId01, _) = await _operations.Send(myObject, _sut, false);

    _objId01.ShouldNotBeNull();

    var objsPulled = await _operations.Receive(_objId01);

      ((List<object>)((Dictionary<string, object>)objsPulled["@dictionary"].NotNull())["a"]).First().ShouldBe(1);
    ((List<object>)objsPulled["@list"].NotNull()).Last().ShouldBe("ciao");
  }

  private async Task UploadDownloadNonCommitObject()
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

    _objId01.ShouldNotBeNull();

    var objPulled = await _operations.Receive(_objId01);

    objPulled.ShouldBeAssignableTo<Base>();

    // Note: even if the layers were originally declared as lists of "Base" objects, on deserialisation we cannot know that,
    // as it's a dynamic property. Dynamic properties, if their content value is ambigous, will default to a common-sense standard.
    // This specifically manifests in the case of lists and dictionaries: List<AnySpecificType> will become List<object>, and
    // Dictionary<string, MyType> will deserialize to Dictionary<string,object>.
    var layerA = (List<object>)((dynamic)objPulled)["LayerA"];
    layerA.Count.ShouldBe(30);

    var layerC = (List<object>)((dynamic)objPulled)["@LayerC"];
    layerC.Count.ShouldBe(30);
   layerC[0].ShouldBeAssignableTo<Point>();

    var layerD = (List<object>)((dynamic)objPulled)["@LayerD"];
    layerD.Count.ShouldBe(2);
  }

  private async Task UploadProgressReports()
  {
    Base myObject = new() { ["items"] = new List<Base>() };
    var rand = new Random();

    for (int i = 0; i < 30; i++)
    {
      ((List<Base>)myObject["items"].NotNull()).Add(
        new Point(i, i, i + rand.NextDouble()) { applicationId = i + "-fab/---" }
      );
    }

    (_commitId02, _) = await _operations.Send(myObject, _sut, false);
  }

  private async Task DownloadProgressReports()
  {
    ProgressArgs? progress = null;
    await _operations.Receive(
      _commitId02.NotNull(),
      onProgressAction: new UnitTestProgress<ProgressArgs>(x =>
      {
        progress = x;
      })
    );
    progress.ShouldNotBeNull();
  }

  [Fact]
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
