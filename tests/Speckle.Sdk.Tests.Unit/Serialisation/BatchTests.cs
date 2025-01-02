using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

[TestFixture]
public class BatchTests
{
  private class BatchItem : IHasSize
  {
    public BatchItem(int size)
    {
      Size = size;
    }

    public int Size { get; }
  }

  [Test]
  public void TestBatchSize_Calc()
  {
    using var batch = new Batch<BatchItem>();
    batch.Add(new BatchItem(1));
    batch.Size.ShouldBe(1);
    batch.Add(new BatchItem(2));
    batch.Size.ShouldBe(3);
  }

  [Test]
  public void TestBatchSize_Trim()
  {
    using var batch = new Batch<BatchItem>();
    batch.Add(new BatchItem(1));
    batch.Add(new BatchItem(2));
    batch.Size.ShouldBe(3);

    batch.Items.Capacity.ShouldBe(Pools.DefaultCapacity);
    batch.TrimExcess();

    batch.Items.Capacity.ShouldBe(2);
    batch.Size.ShouldBe(3);
  }
}
