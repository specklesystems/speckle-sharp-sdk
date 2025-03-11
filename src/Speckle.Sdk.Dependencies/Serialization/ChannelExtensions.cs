using System.Buffers;
using System.Threading.Channels;
using Open.ChannelExtensions;

namespace Speckle.Sdk.Serialisation.V2.Send;

public static class ChannelExtensions
{
  public static BatchingChannelReader<T, IMemoryOwner<T>> BatchByByteSize<T>(
    this ChannelReader<T> source,
    Action<string> logAsWarning,
    int batchSize,
    bool singleReader = false,
    bool allowSynchronousContinuations = false
  )
    where T : IHasByteSize =>
    new SizeBatchingChannelReader<T>(
      source ?? throw new ArgumentNullException(nameof(source)),
      logAsWarning,
      batchSize,
      singleReader,
      allowSynchronousContinuations
    );
}
