using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public static class IdGenerator
{
  [Pure]
  public static Id ComputeId(Json serialized)
  {
#if NET6_0_OR_GREATER
    string hash = Sha256.GetString(serialized.Value.AsSpan(), length: HashUtility.HASH_LENGTH);
#else
    string hash = Sha256.GetString(serialized.Value, length: HashUtility.HASH_LENGTH);
#endif
    return new Id(hash);
  }

  [Pure]
  public static string ComputeId(ReadOnlySpan<byte> input)
  {
    Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
    SHA256.HashData(input, hash);

    Span<char> output = stackalloc char[32];

    for (int i = 0, j = 0; j < 32; i += sizeof(byte), j += sizeof(char))
    {
      hash[i].TryFormat(output[j..], out _, "x2");
    }

    return new string(output);
  }
}
