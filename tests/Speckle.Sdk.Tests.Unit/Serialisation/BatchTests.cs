using FluentAssertions;
using Speckle.Sdk.Serialisation.V2.Send;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

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

  [Fact]
  public void TestBatchSize_Calc()
  {
    using var batch = new Batch<BatchItem>();
    batch.Add(new BatchItem(1));
    batch.Size.Should().Be(1);
    batch.Add(new BatchItem(2));
    batch.Size.Should().Be(3);
  }

  [Fact]
  public void TestBatchSize_Trim()
  {
    using var batch = new Batch<BatchItem>();
    batch.Add(new BatchItem(1));
    batch.Add(new BatchItem(2));
    batch.Size.Should().Be(3);

    batch.Items.Capacity.ShouldBe(Pools.DefaultCapacity);
    batch.TrimExcess();

    batch.Items.Capacity.Should().Be(2);
    batch.Size.Should().Be(3);
  }
}
