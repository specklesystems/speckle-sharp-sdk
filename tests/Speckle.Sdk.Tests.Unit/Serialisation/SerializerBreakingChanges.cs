using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

/// <summary>
/// Test class that documents what property typing changes break backwards/cross/forwards compatibility,
/// and are "breaking" changes.
/// This doesn't guarantee things work this way for SpecklePy
/// Nor does it encompass other tricks (like deserialize callback, or computed json ignored properties)
/// </summary>
public class SerializerBreakingChanges : PrimitiveTestFixture
{
  private readonly IOperations _operations;

  // xUnit does not support a Setup method; instead, you can use the constructor for initialization.
  public SerializerBreakingChanges()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Fact]
  public async Task StringToInt_ShouldThrow()
  {
    var from = new StringValueMock { value = "testValue" };

    await FluentActions
      .Invoking(async () => await from.SerializeAsTAndDeserialize<IntValueMock>(_operations))
      .Should()
      .ThrowAsync<SpeckleDeserializeException>();
  }

  [Theory]
  [MemberData(nameof(MyEnums))]
  public async Task StringToEnum_ShouldThrow(MyEnum testCase)
  {
    var from = new StringValueMock { value = testCase.ToString() };

    await FluentActions
      .Invoking(async () => await from.SerializeAsTAndDeserialize<EnumValueMock>(_operations))
      .Should()
      .ThrowAsync<SpeckleDeserializeException>();
  }

  [Theory(DisplayName = "Deserialization of a JTokenType.Float to a .NET short/int/long should throw exception")]
  [MemberData(nameof(Float64TestCases))]
  [InlineData(1e+30)]
  public async Task DoubleToInt_ShouldThrow(double testCase)
  {
    var from = new DoubleValueMock { value = testCase };

    // ReSharper disable once CompareOfFloatsByEqualityOperator
    bool isInteger = testCase == unchecked((long)testCase);
    if (isInteger)
    {
      var v = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
      Assert.Equal((long)from.value, v.value);
    }
    else
    {
      await FluentActions
        .Invoking(async () => await from.SerializeAsTAndDeserialize<IntValueMock>(_operations))
        .Should()
        .ThrowAsync<SpeckleDeserializeException>();
    }
  }
}
