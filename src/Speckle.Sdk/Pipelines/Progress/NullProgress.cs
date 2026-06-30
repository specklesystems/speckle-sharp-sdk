namespace Speckle.Sdk.Pipelines.Progress;

public sealed class NullProgress<T> : IProgress<T>
{
  public void Report(T value) { }
}
