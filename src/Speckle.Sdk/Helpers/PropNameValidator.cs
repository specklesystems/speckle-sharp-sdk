using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;

namespace Speckle.Sdk.Helpers;

public static
#if NET7_0_OR_GREATER
partial
#endif
class PropNameValidator
{
  private const string CHUNK_PROPERTY_NAME_REGEX_STRING = @"^@\((\d*)\)";

#if NET7_0_OR_GREATER
  [GeneratedRegex(CHUNK_PROPERTY_NAME_REGEX_STRING)]
  private static partial Regex ChunkRegex();

  private static readonly Regex ChunkPropertyNameRegex = ChunkRegex();
#else
  private static readonly Regex ChunkPropertyNameRegex = new(CHUNK_PROPERTY_NAME_REGEX_STRING);
#endif

  public static bool IsChunkable(string propName, out int chunkSize)
  {
    if (ChunkPropertyNameRegex.IsMatch(propName))
    {
      var match = ChunkPropertyNameRegex.Match(propName);
      var isChunkable = int.TryParse(match.Groups[^1].Value, out chunkSize);
      return isChunkable;
    }

    chunkSize = -1;
    return false;
  }

  [Pure]
  public static bool IsDetached(string propName) =>
#if NETSTANDARD2_0
    propName.StartsWith("@");
#else
    propName.StartsWith('@');
#endif
}
