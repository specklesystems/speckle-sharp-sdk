using System.Drawing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Matrix4x4 = Speckle.DoubleNumerics.Matrix4x4;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SerializerNonBreakingChanges : PrimitiveTestFixture
{
  private readonly IOperations _operations;

  public SerializerNonBreakingChanges()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider(typeof(StringValueMock).Assembly);
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Theory, MemberData(nameof(Int8TestCases)), MemberData(nameof(Int32TestCases))]
  public async Task IntToColor(int argb)
  {
    var from = new IntValueMock { value = argb };

    var res = await from.SerializeAsTAndDeserialize<ColorValueMock>(_operations);
    res.value.ToArgb().Should().Be(argb);
  }

  [Theory, MemberData(nameof(Int8TestCases)), MemberData(nameof(Int32TestCases))]
  public async Task ColorToInt(int argb)
  {
    var from = new ColorValueMock { value = Color.FromArgb(argb) };

    var res = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
    res.value.Should().Be(argb);
  }

  [Theory, MemberData(nameof(Int8TestCases)), MemberData(nameof(Int32TestCases)), MemberData(nameof(Int64TestCases))]
  public async Task IntToDouble(long testCase)
  {
    var from = new IntValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    res.value.Should().Be(testCase);
  }

  [Fact]
  public async Task NullToInt()
  {
    var from = new ObjectValueMock { value = null };

    var res = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
    res.value.Should().Be(default(int));
  }

  [Fact]
  public async Task NullToDouble()
  {
    var from = new ObjectValueMock { value = null };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    res.value.Should().Be(0);
  }

  [Theory]
  [MemberData(nameof(UInt64TestCases))]
  public async Task UIntToDouble(ulong testCase)
  {
    var from = new UIntValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    res.value.Should().BeApproximately(testCase, 2048);
  }

  [Theory, MemberData(nameof(Int8TestCases)), MemberData(nameof(Int32TestCases)), MemberData(nameof(Int64TestCases))]
  public async Task IntToString(long testCase)
  {
    var from = new IntValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<StringValueMock>(_operations);
    res.value.Should().Be(testCase.ToString());
  }

  public static IEnumerable<object[]> s_arrayTestCases =>
    new object[]
    {
      Array.Empty<double>(),
      new double[] { 0, 1, int.MaxValue, int.MinValue },
      new[] { default, double.Epsilon, double.MaxValue, double.MinValue },
    }.Select(x => new[] { x });

  [Theory, MemberData(nameof(s_arrayTestCases))]
  public async Task ArrayToList(double[] testCase)
  {
    var from = new ArrayDoubleValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    res.value.Should().BeEquivalentTo(testCase);
  }

  [Theory, MemberData(nameof(s_arrayTestCases))]
  public async Task ListToArray(double[] testCase)
  {
    var from = new ListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<ArrayDoubleValueMock>(_operations);
    res.value.Should().BeEquivalentTo(testCase);
  }

  [Theory, MemberData(nameof(s_arrayTestCases))]
  public async Task ListToIList(double[] testCase)
  {
    var from = new ListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<IReadOnlyListDoubleValueMock>(_operations);
    res.value.Should().BeEquivalentTo(testCase);
  }

  [Theory, MemberData(nameof(s_arrayTestCases))]
  public async Task ListToIReadOnlyList(double[] testCase)
  {
    var from = new ListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<IListDoubleValueMock>(_operations);
    res.value.Should().BeEquivalentTo(testCase);
  }

  [Theory, MemberData(nameof(s_arrayTestCases))]
  public async Task IListToList(double[] testCase)
  {
    var from = new IListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    res.value.Should().BeEquivalentTo(testCase);
  }

  [Theory, MemberData(nameof(s_arrayTestCases))]
  public async Task IReadOnlyListToList(double[] testCase)
  {
    var from = new IReadOnlyListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    res.value.Should().BeEquivalentTo(testCase);
  }

  [Theory, MemberData(nameof(MyEnums))]
  public async Task EnumToInt(MyEnum testCase)
  {
    var from = new EnumValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
    res.value.Should().Be((int)testCase);
  }

  [Theory, MemberData(nameof(MyEnums))]
  public async Task IntToEnum(MyEnum testCase)
  {
    var from = new IntValueMock { value = (int)testCase };

    var res = await from.SerializeAsTAndDeserialize<EnumValueMock>(_operations);
    res.value.Should().Be(testCase);
  }

  [Theory, MemberData(nameof(Float32TestCases)), MemberData(nameof(Float64TestCases))]
  public async Task DoubleToDouble(double testCase)
  {
    var from = new DoubleValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    res.value.Should().Be(testCase);
  }

  [Theory]
  [InlineData(123, 255)]
  [InlineData(256, 1)]
  [InlineData(256, float.MinValue)]
  public async Task ListToMatrix64(int seed, double scalar)
  {
    Random rand = new(seed);
    List<double> testCase = Enumerable.Range(0, 16).Select(_ => rand.NextDouble() * scalar).ToList();

    ListDoubleValueMock from = new() { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<Matrix64ValueMock>(_operations);
    res.value.M11.Should().Be(testCase[0]);
    res.value.M44.Should().Be(testCase[^1]);

    var backAgain = await res.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    backAgain.value.Should().NotBeNull();
    backAgain.value.Should().BeEquivalentTo(testCase);
  }

  [Theory]
  [InlineData(123, 255)]
  [InlineData(256, 1)]
  public async Task Matrix32ToMatrix64(int seed, float scalar)
  {
    Random rand = new(seed);
    List<double> testCase = Enumerable.Range(0, 16).Select(_ => rand.NextDouble() * scalar).ToList();

    ListDoubleValueMock from = new() { value = testCase };

    await FluentActions
      .Invoking(async () => await from.SerializeAsTAndDeserialize<Matrix32ValueMock>(_operations))
      .Should()
      .ThrowAsync<SpeckleDeserializeException>();
  }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.TValueMock")]
public class TValueMock<T> : SerializerMock
{
  public T value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.ListDoubleValueMock")]
public class ListDoubleValueMock : SerializerMock
{
  public List<double> value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.IListDoubleValueMock")]
public class IListDoubleValueMock : SerializerMock
{
  public IList<double> value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.IReadOnlyListDoubleValueMock")]
public class IReadOnlyListDoubleValueMock : SerializerMock
{
  public IReadOnlyList<double> value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.ArrayDoubleValueMock")]
public class ArrayDoubleValueMock : SerializerMock
{
  public double[] value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.IntValueMock")]
public class IntValueMock : SerializerMock
{
  public long value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.IntValueMock")]
public class UIntValueMock : SerializerMock
{
  public ulong value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.StringValueMock")]
public class StringValueMock : SerializerMock
{
  public string value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.DoubleValueMock")]
public class DoubleValueMock : SerializerMock
{
  public double value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.Matrix64ValueMock")]
public class Matrix64ValueMock : SerializerMock
{
  public Matrix4x4 value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.Matrix32ValueMock")]
public class Matrix32ValueMock : SerializerMock
{
  public System.Numerics.Matrix4x4 value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.ColorValueMock")]
public class ColorValueMock : SerializerMock
{
  public Color value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.EnumValueMock")]
public class EnumValueMock : SerializerMock
{
  public MyEnum value { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Serialisation.ObjectValueMock")]
public class ObjectValueMock : SerializerMock
{
  public object? value { get; set; }
}

public enum MyEnum
{
  Zero,
  One,
  Two,
  Three,
  Neg = -1,
  Min = int.MinValue,
  Max = int.MaxValue,
}

public abstract class SerializerMock : Base
{
  private string _speckle_type;

  protected SerializerMock()
  {
    _speckle_type = base.speckle_type;
  }

  public override string speckle_type => _speckle_type;

  public void SerializeAs<T>()
    where T : Base, new()
  {
    T target = new();
    _speckle_type = target.speckle_type;
  }

  internal async Task<TTo> SerializeAsTAndDeserialize<TTo>(IOperations operations)
    where TTo : Base, new()
  {
    SerializeAs<TTo>();

    var json = operations.Serialize(this);

    Base result = await operations.DeserializeAsync(json);
    result.Should().NotBeNull();
    result.Should().BeOfType<TTo>();
    return (TTo)result;
  }
}
