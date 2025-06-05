using System.Diagnostics.Contracts;
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
}
