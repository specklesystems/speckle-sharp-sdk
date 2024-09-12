using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Tests.Unit.Host;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

/// <summary>
/// Test fixture that documents what property typing changes break backwards/cross/forwards compatibility, and are "breaking" changes.
/// This doesn't guarantee things work this way for SpecklePy
/// Nor does it encompass other tricks (like deserialize callback, or computed json ignored properties)
/// </summary>
[TestFixture]
[Description(
  "For certain types, changing property from one type to another is a breaking change, and not backwards/forwards compatible"
)]
public class SerializerBreakingChanges : PrimitiveTestFixture
{
  private IOperations _operations;

  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new SpeckleConfiguration(HostApplications.Navisworks, HostAppVersion.v2023));
    var serviceProvider = serviceCollection.BuildServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Test]
  public void StringToInt_ShouldThrow()
  {
    var from = new StringValueMock { value = "testValue" };

    Assert.ThrowsAsync<SpeckleDeserializeException>(
      async () => await from.SerializeAsTAndDeserialize<IntValueMock>(_operations)
    );
  }

  [Test, TestCaseSource(nameof(MyEnums))]
  public void StringToEnum_ShouldThrow(MyEnum testCase)
  {
    var from = new StringValueMock { value = testCase.ToString() };

    Assert.ThrowsAsync<SpeckleDeserializeException>(async () =>
    {
      var res = await from.SerializeAsTAndDeserialize<EnumValueMock>(_operations);
    });
  }

  [
    Test,
    Description("Deserialization of a JTokenType.Float to a .NET short/int/long should throw exception"),
    TestCaseSource(nameof(Float64TestCases)),
    TestCase(1e+30)
  ]
  public void DoubleToInt_ShouldThrow(double testCase)
  {
    var from = new DoubleValueMock { value = testCase };
    Assert.ThrowsAsync<SpeckleDeserializeException>(
      async () => await from.SerializeAsTAndDeserialize<IntValueMock>(_operations)
    );
  }
}
