using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Send;

public static class ChannelExtensions
{
  public static BatchingChannelReader<T, IMemoryOwner<T>> BatchBySize<T>(
    this ChannelReader<T> source,
    int batchSize,
    bool singleReader = false,
    bool allowSynchronousContinuations = false
  )
    where T : IHasSize =>
    new SizeBatchingChannelReader<T>(
      source ?? throw new ArgumentNullException(nameof(source)),
      batchSize,
      singleReader,
      allowSynchronousContinuations
    );
}
