namespace Speckle.Sdk.Pipelines.Progress;

internal class PipelineProgress(IProgress<CardProgress> progress)
  : IProgress<StreamProgressArgs>,
    IProgress<ConversionProgressArgs>
{
  public void Report(StreamProgressArgs value)
  {
    var (suffix, scaleFactor) = GetFileSizeRendering(value.ExpectedTotalBytes);
    progress.Report(
      new(
        $"Streaming data ({value.BytesStreamed * scaleFactor}{suffix}/{value.ExpectedTotalBytes * scaleFactor}{suffix})",
        (double)value.BytesStreamed / value.ExpectedTotalBytes
      )
    );
  }

  public void Report(ConversionProgressArgs value)
  {
    progress.Report(
      new(
        $"Converting Objects {value.ObjectsConverted}/{value.TotalObjectsToConvert}",
        (double)value.ObjectsConverted / value.TotalObjectsToConvert
      )
    );
  }

  private static readonly string[] s_suffixes = ["Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

  private static (string suffix, double scaleFactor) GetFileSizeRendering(long value)
  {
    if (value <= 0)
    {
      return ("Bytes", 1d);
    }

    for (int i = 0; i < s_suffixes.Length; i++)
    {
      if (value <= Math.Pow(1024, i + 1))
      {
        return (s_suffixes[i], 1 / Math.Pow(1024, i));
      }
    }

    throw new InvalidOperationException("value is too large to convert to a file size");
  }
}
