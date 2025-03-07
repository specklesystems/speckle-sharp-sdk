using System.Diagnostics.Contracts;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public static class IdGenerator
{
  [Pure]
  public static Id ComputeId(Json serialized)
  {
#if NET6_0_OR_GREATER
    string hash = Crypt.Sha256(serialized.Value.AsSpan(), length: HashUtility.HASH_LENGTH);
#else
    string hash = Crypt.Sha256(serialized.Value, length: HashUtility.HASH_LENGTH);
#endif
    return new Id(hash);
  }
}
