#if NETSTANDARD2_0
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Speckle.Sdk.Serialisation.Receive;

public static class Extensions {
  public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this ChannelReader<T> reader, CancellationToken cancellationToken = default)
  {
    if (reader is null)
    {
      throw new ArgumentNullException(nameof(reader));
    }

    Contract.EndContractBlock();

    return AsAsyncEnumerableCore(reader, cancellationToken);

    static async IAsyncEnumerable<T> AsAsyncEnumerableCore(ChannelReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
      do
      {
        while (!cancellationToken.IsCancellationRequested && reader.TryRead(out T? item))
        {
          yield return item;
        }
      }
      while (
        !cancellationToken.IsCancellationRequested
        && await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));
    }
  }
}
#endif
