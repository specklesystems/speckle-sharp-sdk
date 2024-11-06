using System.Threading.Channels;
using Open.ChannelExtensions;
using Speckle.Sdk.Dependencies.Serialization;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class SizeBatchingChannelReader(
  ChannelReader<BaseItem> source,
  int batchSize,
  bool singleReader,
  bool syncCont = false
) : BatchingChannelReader<BaseItem, List<BaseItem>>(source, batchSize, singleReader, syncCont)
{
  protected override List<BaseItem> CreateBatch(int capacity) => new();

  protected override void TrimBatch(List<BaseItem> batch) => batch.TrimExcess();

  protected override void AddBatchItem(List<BaseItem> batch, BaseItem item) => batch.Add(item);

  protected override int GetBatchSize(List<BaseItem> batch)
  {
    int size = 0;
    foreach (BaseItem item in batch)
    {
      size += item.Size;
    }
    return size;
  }
}
