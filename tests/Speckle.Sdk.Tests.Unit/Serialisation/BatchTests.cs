using FluentAssertions;
using NUnit.Framework;
using Shouldly;
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
    var batch = new Batch<BatchItem>(4);
    batch.Add(new BatchItem(1));
    batch.Size.Should().Be(1);
    batch.Add(new BatchItem(2));
    batch.Size.Should().Be(3);
  }

  [Fact]
  public void TestBatchSize_Trim()
  {
    var batch = new Batch<BatchItem>(4);
    batch.Add(new BatchItem(1));
    batch.Add(new BatchItem(2));
    batch.Size.Should().Be(3);

    batch.Items.Capacity.Should().Be(4);
    batch.TrimExcess();

    batch.Items.Capacity.Should().Be(2);
    batch.Size.Should().Be(3);
  }
}
