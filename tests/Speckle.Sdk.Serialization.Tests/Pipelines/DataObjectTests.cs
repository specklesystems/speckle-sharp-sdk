using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Serialization.Tests.Pipelines;

public class DataObjectTests
{
  private readonly Serializer _sut;

  public DataObjectTests()
  {
    TypeLoader.Initialize(typeof(TestClass).Assembly, typeof(Polyline).Assembly);

    _sut = new();
  }

  [Theory]
  [InlineData(typeof(ArcgisObject))]
  [InlineData(typeof(ArchicadObject))]
  [InlineData(typeof(Civil3dObject))]
  [InlineData(typeof(DataObject))]
  [InlineData(typeof(EtabsObject))]
  [InlineData(typeof(NavisworksObject))]
  [InlineData(typeof(RevitObject))]
  [InlineData(typeof(TeklaObject))]
  public async Task ValidateDataObject(Type type)
  {
    Base myBase = (Base)(
      Activator.CreateInstance(type) ?? throw new Exception("Could not create instance of " + type.Name)
    );
    IEnumerable<UploadItem> result = _sut.Serialize(myBase);

    var json = result.Last().Json.ToJsonString();

    await VerifyJson(json).UseParameters(type);
  }
}
