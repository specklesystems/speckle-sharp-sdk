#if NETSTANDARD2_0
using System.ComponentModel;

namespace Speckle.Sdk.Common;

/// <summary>
/// A hash code used to help with implementing <see cref="object.GetHashCode()"/>.
/// </summary>
public readonly struct HashCode : IEquatable<HashCode>
{
  private const int EMPTY_COLLECTION_PRIME_NUMBER = 19;
  private readonly int _value;

  /// <summary>
  /// Initializes a new instance of the <see cref="HashCode"/> struct.
  /// </summary>
  /// <param name="value">The value.</param>
  private HashCode(int value) => _value = value;

  /// <summary>
  /// Performs an implicit conversion from <see cref="HashCode"/> to <see cref="int"/>.
  /// </summary>
  /// <param name="hashCode">The hash code.</param>
  /// <returns>The result of the conversion.</returns>
  public static implicit operator int(HashCode hashCode) => hashCode.ToInt32();

  /// <summary>
  /// Implements the operator ==.
  /// </summary>
  /// <param name="left">The left.</param>
  /// <param name="right">The right.</param>
  /// <returns>The result of the operator.</returns>
  public static bool operator ==(HashCode left, HashCode right) => left.Equals(right);

  /// <summary>
  /// Implements the operator !=.
  /// </summary>
  /// <param name="left">The left.</param>
  /// <param name="right">The right.</param>
  /// <returns>The result of the operator.</returns>
  public static bool operator !=(HashCode left, HashCode right) => !(left == right);

  /// <summary>
  /// Takes the hash code of the specified item.
  /// </summary>
  /// <typeparam name="T">The type of the item.</typeparam>
  /// <param name="item">The item.</param>
  /// <returns>The new hash code.</returns>
  public static HashCode Of<T>(T item) => new HashCode(GetHashCode(item));

  /// <summary>
  /// Takes the hash code of the specified items.
  /// </summary>
  /// <typeparam name="T">The type of the items.</typeparam>
  /// <param name="items">The collection.</param>
  /// <returns>The new hash code.</returns>
  public static HashCode OfEach<T>(IEnumerable<T>? items) =>
    items == null ? new HashCode(0) : new HashCode(GetHashCode(items, 0));

  /// <summary>
  /// Adds the hash code of the specified item.
  /// </summary>
  /// <typeparam name="T">The type of the item.</typeparam>
  /// <param name="item">The item.</param>
  /// <returns>The new hash code.</returns>
  public HashCode And<T>(T item) => new HashCode(CombineHashCodes(this._value, GetHashCode(item)));

  /// <summary>
  /// Adds the hash code of the specified items in the collection.
  /// </summary>
  /// <typeparam name="T">The type of the items.</typeparam>
  /// <param name="items">The collection.</param>
  /// <returns>The new hash code.</returns>
  public HashCode AndEach<T>(IEnumerable<T>? items)
  {
    if (items == null)
    {
      return new HashCode(this._value);
    }

    return new HashCode(GetHashCode(items, this._value));
  }

  /// <inheritdoc />
  public bool Equals(HashCode other) => this._value.Equals(other._value);

  /// <inheritdoc />
  public override bool Equals(object? obj)
  {
    if (obj is HashCode code)
    {
      return this.Equals(code);
    }

    return false;
  }

  [EditorBrowsable(EditorBrowsableState.Never)]
  public override int GetHashCode() => ToInt32();

  private static int CombineHashCodes(int h1, int h2)
  {
    unchecked
    {
      // Code copied from System.Tuple so it must be the best way to combine hash codes or at least a good one.
      return ((h1 << 5) + h1) ^ h2;
    }
  }

  private static int GetHashCode<T>(T item) => item?.GetHashCode() ?? 0;

  private static int GetHashCode<T>(IEnumerable<T> items, int startHashCode)
  {
    var temp = startHashCode;

    using var enumerator = items.GetEnumerator();
    if (enumerator.MoveNext())
    {
      temp = CombineHashCodes(temp, GetHashCode(enumerator.Current));

      while (enumerator.MoveNext())
      {
        temp = CombineHashCodes(temp, GetHashCode(enumerator.Current));
      }
    }
    else
    {
      temp = CombineHashCodes(temp, EMPTY_COLLECTION_PRIME_NUMBER);
    }

    return temp;
  }

  public int ToInt32()
  {
    return _value;
  }
}
#endif
