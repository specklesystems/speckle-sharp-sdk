using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Matrix4x4 = Speckle.DoubleNumerics.Matrix4x4;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

/// <summary>
/// Test fixture that documents what property typing changes maintain backwards/cross/forwards compatibility, and are "non-breaking" changes.
/// This doesn't guarantee things work this way for SpecklePy
/// Nor does it encompass other tricks (like deserialize callback, or computed json ignored properties)
/// </summary>
[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class SerializerNonBreakingChanges : PrimitiveTestFixture
{
  private IOperations _operations;

  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(StringValueMock).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Test, TestCaseSource(nameof(Int8TestCases)), TestCaseSource(nameof(Int32TestCases))]
  public async Task IntToColor(int argb)
  {
    var from = new IntValueMock { value = argb };

    var res = await from.SerializeAsTAndDeserialize<ColorValueMock>(_operations);
    Assert.That(res.value.ToArgb(), Is.EqualTo(argb));
  }

  [Test, TestCaseSource(nameof(Int8TestCases)), TestCaseSource(nameof(Int32TestCases))]
  public async Task ColorToInt(int argb)
  {
    var from = new ColorValueMock { value = Color.FromArgb(argb) };

    var res = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(argb));
  }

  [
    Test,
    TestCaseSource(nameof(Int8TestCases)),
    TestCaseSource(nameof(Int32TestCases)),
    TestCaseSource(nameof(Int64TestCases))
  ]
  public async Task IntToDouble(long testCase)
  {
    var from = new IntValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(testCase));
  }

  [Test]
  public async Task NullToInt()
  {
    var from = new ObjectValueMock { value = null };

    var res = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(default(int)));
  }

  [Test]
  public async Task NullToDouble()
  {
    var from = new ObjectValueMock { value = null };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(default(double)));
  }

  // IMPORTANT!!: This test mimics large numbers that we sometimes see from python
  // This is behaviour our deserializer has, but not necessarily commited to keeping
  // Numbers outside the range of a Long are not officially supported
  [Test]
  [TestCaseSource(nameof(UInt64TestCases))]
  [DefaultFloatingPointTolerance(2048)]
  public async Task UIntToDouble(ulong testCase)
  {
    var from = new UIntValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(testCase));
  }

  [
    Test,
    TestCaseSource(nameof(Int8TestCases)),
    TestCaseSource(nameof(Int32TestCases)),
    TestCaseSource(nameof(Int64TestCases))
  ]
  public async Task IntToString(long testCase)
  {
    var from = new IntValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<StringValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(testCase.ToString()));
  }

  private static readonly double[][] s_arrayTestCases =
  {
    Array.Empty<double>(),
    new double[] { 0, 1, int.MaxValue, int.MinValue },
    new[] { default, double.Epsilon, double.MaxValue, double.MinValue },
  };

  [Test, TestCaseSource(nameof(s_arrayTestCases))]
  public async Task ArrayToList(double[] testCase)
  {
    var from = new ArrayDoubleValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    Assert.That(res.value, Is.EquivalentTo(testCase));
  }

  [Test, TestCaseSource(nameof(s_arrayTestCases))]
  public async Task ListToArray(double[] testCase)
  {
    var from = new ListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<ArrayDoubleValueMock>(_operations);
    Assert.That(res.value, Is.EquivalentTo(testCase));
  }

  [Test, TestCaseSource(nameof(s_arrayTestCases))]
  public async Task ListToIList(double[] testCase)
  {
    var from = new ListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<IReadOnlyListDoubleValueMock>(_operations);
    Assert.That(res.value, Is.EquivalentTo(testCase));
  }

  [Test, TestCaseSource(nameof(s_arrayTestCases))]
  public async Task ListToIReadOnlyList(double[] testCase)
  {
    var from = new ListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<IListDoubleValueMock>(_operations);
    Assert.That(res.value, Is.EquivalentTo(testCase));
  }

  [Test, TestCaseSource(nameof(s_arrayTestCases))]
  public async Task IListToList(double[] testCase)
  {
    var from = new IListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    Assert.That(res.value, Is.EquivalentTo(testCase));
  }

  [Test, TestCaseSource(nameof(s_arrayTestCases))]
  public async Task IReadOnlyListToList(double[] testCase)
  {
    var from = new IReadOnlyListDoubleValueMock { value = testCase.ToList() };

    var res = await from.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    Assert.That(res.value, Is.EquivalentTo(testCase));
  }

  [Test, TestCaseSource(nameof(MyEnums))]
  public async Task EnumToInt(MyEnum testCase)
  {
    var from = new EnumValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<IntValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo((int)testCase));
  }

  [Test, TestCaseSource(nameof(MyEnums))]
  public async Task IntToEnum(MyEnum testCase)
  {
    var from = new IntValueMock { value = (int)testCase };

    var res = await from.SerializeAsTAndDeserialize<EnumValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(testCase));
  }

  [Test]
  [TestCaseSource(nameof(Float64TestCases))]
  [TestCaseSource(nameof(Float32TestCases))]
  public async Task DoubleToDouble(double testCase)
  {
    var from = new DoubleValueMock { value = testCase };

    var res = await from.SerializeAsTAndDeserialize<DoubleValueMock>(_operations);
    Assert.That(res.value, Is.EqualTo(testCase));
  }

  [Test]
  [TestCase(123, 255)]
  [TestCase(256, 1)]
  [TestCase(256, float.MinValue)]
  public async Task ListToMatrix64(int seed, double scalar)
  {
    Random rand = new(seed);
    List<double> testCase = Enumerable.Range(0, 16).Select(_ => rand.NextDouble() * scalar).ToList();

    ListDoubleValueMock from = new() { value = testCase };

    //Test List -> Matrix
    var res = await from.SerializeAsTAndDeserialize<Matrix64ValueMock>(_operations);
    Assert.That(res.value.M11, Is.EqualTo(testCase[0]));
    Assert.That(res.value.M44, Is.EqualTo(testCase[testCase.Count - 1]));

    //Test Matrix -> List
    var backAgain = await res.SerializeAsTAndDeserialize<ListDoubleValueMock>(_operations);
    Assert.That(backAgain.value, Is.Not.Null);
    Assert.That(backAgain.value, Is.EquivalentTo(testCase));
  }

  [Test]
  [TestCase(123, 255)]
  [TestCase(256, 1)]
  [DefaultFloatingPointTolerance(Constants.EPS)]
  public void Matrix32ToMatrix64(int seed, float scalar)
  {
    Random rand = new(seed);
    List<double> testCase = Enumerable.Range(0, 16).Select(_ => rand.NextDouble() * scalar).ToList();

    ListDoubleValueMock from = new() { value = testCase };

    //Test List -> Matrix
    var exception = Assert.ThrowsAsync<SpeckleDeserializeException>(
      async () => await from.SerializeAsTAndDeserialize<Matrix32ValueMock>(_operations)
    );
    exception.ShouldNotBeNull();
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
    Assert.That(result, Is.Not.Null);
    Assert.That(result, Is.TypeOf<TTo>());
    return (TTo)result;
  }
}

public abstract class PrimitiveTestFixture
{
  public static readonly sbyte[] Int8TestCases = { default, sbyte.MaxValue, sbyte.MinValue };
  public static readonly short[] Int16TestCases = { short.MaxValue, short.MinValue };
  public static readonly int[] Int32TestCases = { int.MinValue, int.MaxValue };
  public static readonly long[] Int64TestCases = { long.MaxValue, long.MinValue };
  public static readonly ulong[] UInt64TestCases = { ulong.MaxValue, ulong.MinValue };

  public static double[] Float64TestCases { get; } =
    {
      default,
      double.Epsilon,
      double.MaxValue,
      double.MinValue,
      double.PositiveInfinity,
      double.NegativeInfinity,
      double.NaN,
    };

  public static float[] Float32TestCases { get; } =
    {
      default,
      float.Epsilon,
      float.MaxValue,
      float.MinValue,
      float.PositiveInfinity,
      float.NegativeInfinity,
      float.NaN,
    };

  public static Half[] Float16TestCases { get; } =
    { default, Half.Epsilon, Half.MaxValue, Half.MinValue, Half.PositiveInfinity, Half.NegativeInfinity, Half.NaN };

  public static float[] FloatIntegralTestCases { get; } = { 0, 1, int.MaxValue, int.MinValue };

  public static MyEnum[] MyEnums { get; } = Enum.GetValues(typeof(MyEnum)).Cast<MyEnum>().ToArray();
}
