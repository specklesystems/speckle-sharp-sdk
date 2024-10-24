using NUnit.Framework;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Transports;

[TestFixture]
public abstract class TransportTests
{
  protected abstract ITransport? Sut { get; }

  [Test]
  public async Task SaveAndRetrieveObject()
  {
    const string PAYLOAD_ID = "MyTestObjectId";
    const string PAYLOAD_DATA = "MyTestObjectData";

    {
      var preAdd = await Sut.NotNull().GetObject(PAYLOAD_ID);
      Assert.That(preAdd, Is.Null);
    }

    Sut.SaveObject(PAYLOAD_ID, PAYLOAD_DATA);
    await Sut.WriteComplete();

    {
      var postAdd = await Sut.GetObject(PAYLOAD_ID);
      Assert.That(postAdd, Is.EqualTo(PAYLOAD_DATA));
    }
  }

  [Test]
  public async Task HasObject()
  {
    const string PAYLOAD_ID = "MyTestObjectId";
    const string PAYLOAD_DATA = "MyTestObjectData";

    {
      var preAdd = await Sut.NotNull().HasObjects(new[] { PAYLOAD_ID });
      Assert.That(preAdd, Has.Exactly(1).Items);
      Assert.That(preAdd, Has.No.ContainValue(true));
      Assert.That(preAdd, Contains.Key(PAYLOAD_ID));
    }

    Sut.SaveObject(PAYLOAD_ID, PAYLOAD_DATA);
    await Sut.WriteComplete();

    {
      var postAdd = await Sut.HasObjects(new[] { PAYLOAD_ID });

      Assert.That(postAdd, Has.Exactly(1).Items);
      Assert.That(postAdd, Has.No.ContainValue(false));
      Assert.That(postAdd, Contains.Key(PAYLOAD_ID));
    }
  }

  [Test]
  [Description("Test that transports save objects when many threads are concurrently saving data")]
  public async Task SaveObject_ConcurrentWrites()
  {
    const int TEST_DATA_COUNT = 100;
    List<(string id, string data)> testData = Enumerable
      .Range(0, TEST_DATA_COUNT)
      .Select(_ => (Guid.NewGuid().ToString(), Guid.NewGuid().ToString()))
      .ToList();

    Parallel.ForEach(
      testData,
      x =>
      {
        Sut.NotNull().SaveObject(x.id, x.data);
      }
    );

    await Sut.NotNull().WriteComplete();

    //Test 1. SavedObjectCount //WARN: FAIL!!! seems this is not implemented for SQLite Transport
    //Assert.That(transport.SavedObjectCount, Is.EqualTo(testDataCount));

    //Test 2. HasObjects
    var ids = testData.Select(x => x.id).ToList();
    var hasObjectsResult = await Sut.HasObjects(ids);

    Assert.That(hasObjectsResult, Does.Not.ContainValue(false));
    Assert.That(hasObjectsResult.Keys, Is.EquivalentTo(ids));

    //Test 3. GetObjects
    foreach (var x in testData)
    {
      var res = await Sut.GetObject(x.id);
      Assert.That(res, Is.EqualTo(x.data));
    }
  }

  [Test]
  public async Task ProgressAction_Called_OnSaveObject()
  {
    bool wasCalled = false;
    Sut.NotNull().OnProgressAction = new UnitTestProgress<ProgressArgs>(_ => wasCalled = true);

    Sut.SaveObject("12345", "fake payload data");

    await Sut.WriteComplete();

    Assert.That(wasCalled, Is.True);
  }

  [Test]
  public void ToString_IsNotEmpty()
  {
    var toString = Sut.NotNull().ToString();

    Assert.That(toString, Is.Not.Null);
    Assert.That(toString, Is.Not.Empty);
  }

  [Test]
  public void TransportName_IsNotEmpty()
  {
    var toString = Sut.NotNull().TransportName;

    Assert.That(toString, Is.Not.Null);
    Assert.That(toString, Is.Not.Empty);
  }

  [Test]
  public void SaveObject_ExceptionThrown_TaskIsCanceled()
  {
    using CancellationTokenSource tokenSource = new();
    Sut.NotNull().CancellationToken = tokenSource.Token;

    tokenSource.Cancel();

    Assert.CatchAsync<OperationCanceledException>(async () =>
    {
      Sut.SaveObject("abcdef", "fake payload data");
      await Sut.WriteComplete();
    });
  }

  [Test]
  public async Task CopyObjectAndChildren()
  {
    //Assemble
    const int TEST_DATA_COUNT = 100;
    List<(string id, string data)> testData = Enumerable
      .Range(0, TEST_DATA_COUNT)
      .Select(_ => (Guid.NewGuid().ToString(), Guid.NewGuid().ToString()))
      .ToList();

    foreach (var x in testData)
    {
      Sut.NotNull().SaveObject(x.id, x.data);
    }

    var parent = JsonConvert.SerializeObject(
      new TransportHelpers.Placeholder() { __closure = testData.Select(x => x.id).ToDictionary(x => x, _ => 1) }
    );
    Sut.NotNull().SaveObject("root", parent);

    await Sut.WriteComplete();

    // Act
    MemoryTransport destination = new();
    await Sut.CopyObjectAndChildren("root", destination);

    //Assert
    foreach (var (expectedId, expectedData) in testData)
    {
      var actual = await destination.GetObject(expectedId);
      Assert.That(actual, Is.EqualTo(expectedData));
    }
  }
}
