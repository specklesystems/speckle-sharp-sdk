using System.Diagnostics.Contracts;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public static class IdGenerator
{
  [Pure]
  public static string ComputeId(string serialized)
  {
#if NET6_0_OR_GREATER
    string hash = Crypt.Sha256(serialized.AsSpan(), length: HashUtility.HASH_LENGTH);
#else
    string hash = Crypt.Sha256(serialized, length: HashUtility.HASH_LENGTH);
#endif
    return hash;
  }
}
