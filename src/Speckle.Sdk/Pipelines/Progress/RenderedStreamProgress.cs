namespace Speckle.Sdk.Pipelines.Progress;

/// <summary>
/// Renders "low level" data stream updates
/// into "high level" <see cref="CardProgress"/> that is expected by Ingestion progress and DUI3
/// </summary>
/// <param name="progress"></param>
public sealed class RenderedStreamProgress(IProgress<CardProgress> progress) : IProgress<StreamProgressArgs>
{
  public void Report(StreamProgressArgs value)
  {
    var (suffix, scaleFactor) = GetFileSizeRendering(value.ExpectedTotalBytes);
    progress.Report(
      new(
        $"Streaming data ({value.BytesStreamed * scaleFactor:F1}/{value.ExpectedTotalBytes * scaleFactor:F1} {suffix})",
        (double)value.BytesStreamed / value.ExpectedTotalBytes
      )
    );
  }

  private static readonly string[] s_suffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

  private static (string suffix, double scaleFactor) GetFileSizeRendering(long value)
  {
    if (value <= 0)
    {
      return (s_suffixes[0], 1d);
    }

    for (int i = 0; i < s_suffixes.Length; i++)
    {
      if (value <= Math.Pow(1024, i + 1))
      {
        return (s_suffixes[i], 1 / Math.Pow(1024, i));
      }
    }

    throw new ArgumentOutOfRangeException(nameof(value), "Value is too large to convert to a file size");
  }
}
