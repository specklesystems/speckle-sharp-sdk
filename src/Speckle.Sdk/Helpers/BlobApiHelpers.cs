using System.Net.Http.Headers;

namespace Speckle.Sdk.Helpers;

public static class BlobApiHelpers
{
  public static string ParseEtagHeader(HttpResponseHeaders headers)
  {
    if (!headers.TryGetValues("ETag", out var etagValues))
    {
      throw new ArgumentException(
        "Response does not have an ETag attached to it, cannot use this as an upload",
        nameof(headers)
      );
    }

    var etagValuesArray = etagValues.ToArray();

    if (etagValuesArray.Length != 1)
    {
      throw new ArgumentException(
        $"Expected Etag header to have a single value but got {etagValuesArray.Length}",
        nameof(headers)
      );
    }

    return etagValuesArray[0];
  }
}
