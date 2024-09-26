using System.Net;

namespace Speckle.Sdk.Transports.ServerUtils;

public class MultipartJsonStreamContext(List<(string, MemoryStream)> data) : MultipartContent
{
  protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context) 
  {
    using var writer = new StreamWriter(stream);
    writer.Write('[');
    for (int i = 0; i < data.Count; i++)
    {
      if (i > 0)
      {
        writer.Write(',');
      }

      await data[i].Item2.CopyToAsync(stream).ConfigureAwait(false);
    }
    writer.Write(']');
  }
}
