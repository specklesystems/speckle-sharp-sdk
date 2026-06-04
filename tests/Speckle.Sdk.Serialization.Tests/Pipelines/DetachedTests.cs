using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Serialization.Tests.Pipelines;

public class DetachedTests
{
  private readonly Serializer _sut;

  public DetachedTests()
  {
    TypeLoader.ReInitialize(typeof(TestClass).Assembly, typeof(Polyline).Assembly);

    _sut = new();
  }

  [Fact]
  public async Task CanSerialize_New_Detached()
  {
    var myBase = new SampleObjectBase
    {
      ["dynamicProp"] = 123,
      detachedProp = new SamplePropBase() { name = "detachedProp" },
      attachedProp = new SamplePropBase() { name = "attachedProp" },
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task GetPropertiesExpected_Detached()
  {
    var @myBase = new SampleObjectBase
    {
      ["dynamicProp"] = 123,
      ["@prop2"] = 2,
      ["__prop3"] = 3,
      detachedProp = new SamplePropBase() { name = "detachedProp" },
      attachedProp = new SamplePropBase() { name = "attachedProp" },
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task GetPropertiesExpected_All()
  {
    var myBase = new SampleObjectBase
    {
      ["dynamicProp"] = 123,
      ["@prop2"] = 2,
      ["__prop3"] = 3,
      detachedProp = new SamplePropBase() { name = "detachedProp" },
      attachedProp = new SamplePropBase() { name = "attachedProp" },
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_New_Detached2()
  {
    var myBase = new SampleObjectBase2
    {
      ["dynamicProp"] = 123,
      applicationId = "1",
      detachedProp = new SamplePropBase2()
      {
        name = "detachedProp",
        applicationId = "2",
        line = new Polyline() { units = "test", value = [1.0, 2.0] },
      },
      detachedProp2 = new SamplePropBase2()
      {
        name = "detachedProp2",
        applicationId = "3",
        line = new Polyline() { units = "test", value = [3.0, 2.0] },
      },
      attachedProp = new SamplePropBase2()
      {
        name = "attachedProp",
        applicationId = "4",
        line = new Polyline() { units = "test", value = [3.0, 4.0] },
      },
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_Attached()
  {
    var myBase = new SampleObjectBase2
    {
      ["dynamicProp"] = 123,
      applicationId = "1",
      attachedProp = new SamplePropBase2()
      {
        name = "attachedProp",
        applicationId = "4",
        line = new Polyline() { units = "test", value = [3.0, 4.0] },
      },
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_Attached_2()
  {
    var myBase = new SampleObjectBase2
    {
      ["dynamicProp"] = 123,
      applicationId = "1",
      attachedProp = new SamplePropBase2() { name = "attachedProp", applicationId = "4" },
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_New_Detached_With_DataChunks()
  {
    var myBase = new SampleObjectBase2
    {
      ["dynamicProp"] = 123,
      applicationId = "1",
      list = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
      list2 = [1, 10],
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }

  [Fact]
  public async Task CanSerialize_New_Detached_With_DataChunks2()
  {
    var myBase = new SampleObjectBase2
    {
      ["dynamicProp"] = 123,
      applicationId = "1",
      list = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
      list2 = [1, 10],
      arr = [1, 10],
    };

    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var objects = result.ToDictionary(x => x.Id, x => x.Json.ToJsonString());

    await VerifyJsonDictionary(objects);
  }
}
