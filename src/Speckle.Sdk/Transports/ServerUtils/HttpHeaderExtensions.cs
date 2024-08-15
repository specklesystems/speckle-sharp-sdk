using System.Net.Http.Headers;

namespace Speckle.Sdk.Transports.ServerUtils;

public static class HttpHeaderExtensions
{
  public static void CopyTo(this HttpContentHeaders source, HttpContentHeaders destination)
  {
    foreach (var header in source)
    {
      destination.Add(header.Key, header.Value);
    }
  }
}
