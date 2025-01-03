namespace Speckle.Sdk.Tests.Unit.Serialisation;

public abstract class PrimitiveTestFixture
{
  public static  IEnumerable<object[]> Int8TestCases => new sbyte[] { 0, sbyte.MaxValue, sbyte.MinValue }.Select(x => new object[] { x });
  public static readonly short[] Int16TestCases = { short.MaxValue, short.MinValue };
  public static  IEnumerable<object[]> Int32TestCases => new int[] { int.MinValue, int.MaxValue }.Select(x => new object[] { x });

  public static  IEnumerable<object[]> Int64TestCases => new long[] { long.MinValue, long.MaxValue }.Select(x => new object[] { x });

  public static  IEnumerable<object[]> UInt64TestCases => new ulong[] { ulong.MinValue, ulong.MaxValue }.Select(x => new object[] { x });


  
  public static  IEnumerable<object[]> Float64TestCases  => new[]
  {
    0,
    double.Epsilon,
    double.MaxValue,
    double.MinValue,
    double.PositiveInfinity,
    double.NegativeInfinity,
    double.NaN,
  }.Select(x => new object[] { x });

  public static IEnumerable<object[]> Float32TestCases => new[]
  {
    default,
    float.Epsilon,
    float.MaxValue,
    float.MinValue,
    float.PositiveInfinity,
    float.NegativeInfinity,
    float.NaN,
  }.Select(x => new object[] { x });

  public static Half[] Float16TestCases { get; } =
    { default, Half.Epsilon, Half.MaxValue, Half.MinValue, Half.PositiveInfinity, Half.NegativeInfinity, Half.NaN };

  public static float[] FloatIntegralTestCases { get; } = { 0, 1, int.MaxValue, int.MinValue };

  public static IEnumerable<object[]> MyEnums { get; } = Enum.GetValues(typeof(MyEnum)).Cast<object>().Select(x => new[] { x });
}
