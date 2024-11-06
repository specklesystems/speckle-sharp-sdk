using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Dependencies.Serialization;

namespace Speckle.Sdk.Serialisation.V2.Send;

public static class ChannelExtensions
{
  public static BatchingChannelReader<BaseItem, List<BaseItem>> BatchBySize(
    this ChannelReader<BaseItem> source,
    int batchSize,
    bool singleReader = false,
    bool allowSynchronousContinuations = false)
    => new SizeBatchingChannelReader(source ?? throw new ArgumentNullException(nameof(source)), batchSize, singleReader, allowSynchronousContinuations);

}
