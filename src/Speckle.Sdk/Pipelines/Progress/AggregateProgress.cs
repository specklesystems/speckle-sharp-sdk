namespace Speckle.Sdk.Pipelines.Progress;

public sealed class AggregateProgress<T>(params IProgress<T>[] progresses) : IProgress<T>
{
  public void Report(T value)
  {
    foreach (var progress in progresses)
    {
      progress.Report(value);
    }
  }
}
