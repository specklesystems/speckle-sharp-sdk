using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Testing;

public class Progress(bool write) : IProgress<ProgressArgs>
{
  public void Report(ProgressArgs value)
  {
    if (write)
    {
      Console.WriteLine(value.ProgressEvent + " " + value.Count + " objects received." + value.Total);
    }
  }
}
