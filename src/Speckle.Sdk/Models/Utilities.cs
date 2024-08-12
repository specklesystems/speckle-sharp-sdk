using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Models;

public static class Utilities
{
  public enum HashingFunctions
  {
    SHA256,
    MD5
  }

  public static int HashLength => 32;

  /// <summary>
  /// Wrapper method around hashing functions..
  /// </summary>
  /// <param name="input"></param>
  /// <returns></returns>
  [Pure]
  public static string HashString(string input, HashingFunctions func = HashingFunctions.SHA256)
  {
    return func switch
    {
      HashingFunctions.SHA256 => Crypt.Sha256(input, length: HashLength),
      HashingFunctions.MD5 => Crypt.Md5(input, length: HashLength),
      _ => throw new ArgumentOutOfRangeException(nameof(func), func, "Unrecognised value"),
    };
  }

  [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms")]
  public static string HashFile(string filePath, HashingFunctions func = HashingFunctions.SHA256)
  {
    using HashAlgorithm hashAlgorithm = func == HashingFunctions.MD5 ? MD5.Create() : SHA256.Create();

    using var stream = File.OpenRead(filePath);

    var hash = hashAlgorithm.ComputeHash(stream);
    return BitConverter.ToString(hash, 0, HashLength).Replace("-", "").ToLowerInvariant();
  }

  /// <summary>
  /// Utility function to flatten a conversion result that might have nested lists of objects.
  /// This happens, for example, in the case of multiple display value fallbacks for a given object.
  /// </summary>
  /// <remarks>
  ///   Assuming native objects are not inherited from IList.
  /// </remarks>
  /// <param name="item"> Object to flatten</param>
  /// <returns> Flattened objects after to host.</returns>
  public static List<object?> FlattenToHostConversionResult(object? item)
  {
    List<object?> convertedList = new();
    Stack<object?> stack = new();
    stack.Push(item);

    while (stack.Count > 0)
    {
      object? current = stack.Pop();
      if (current is IList list)
      {
        foreach (object? subItem in list)
        {
          stack.Push(subItem);
        }
      }
      else
      {
        convertedList.Add(current);
      }
    }

    return convertedList;
  }
}
