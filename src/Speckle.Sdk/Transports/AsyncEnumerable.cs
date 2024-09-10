namespace Speckle.Sdk.Transports;

public static class AsyncEnumerable
{
  /// <summary>
  /// Creates an <see cref="IAsyncEnumerable{T}"/> which yields no results, similar to <see cref="Enumerable.Empty{TResult}"/>.
  /// </summary>
  public static IAsyncEnumerable<T> Empty<T>() => EmptyAsyncEnumerator<T>.Instance;

  private class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
  {
    public static readonly EmptyAsyncEnumerator<T> Instance = new EmptyAsyncEnumerator<T>();
    public T Current => default!;

    public ValueTask DisposeAsync() => default;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return this;
    }

    public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);
  }
}
