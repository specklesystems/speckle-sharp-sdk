namespace Speckle.Sdk.Common;

public static class AsyncEnumerable
{
  public static async IAsyncEnumerable<T> Empty<T>()
  {
    await Task.CompletedTask.ConfigureAwait(false);
    yield break;
  }
}
