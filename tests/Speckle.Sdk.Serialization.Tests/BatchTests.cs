using FluentAssertions;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class BatchTests
{

  /*** from BatchingChannelReader.cs **
  bool full = GetBatchSize(c) == _batchSize;
  while (!full && source.TryRead(out item))
  {
    AddBatchItem(c, item);
    full = GetBatchSize(c) == _batchSize;
  }*/

  public class TestBatchItem : IHasSize
  {
    public int Size { get; set; }
  }

  [Fact]
  public void Basics()
  {
    using var batch = new Batch<TestBatchItem>();
    batch.Add(new TestBatchItem { Size = 2 });
    batch.Size.Should().Be(2);
    batch.Add(new TestBatchItem { Size = 2 });
    batch.Size.Should().Be(4);
    batch.Add(new TestBatchItem { Size = 2 });
    batch.Size.Should().Be(6);

    batch.TrimExcess();
    batch.Size.Should().Be(6);
    batch.Items.Count.Should().Be(3);
  }

  [Fact]
  public void Large_Message_Problem_1()
  {
    const int MAX_BATCH_SIZE = 5;


    using var batch = new Batch<TestBatchItem>();
    batch.AddBatchItem(new TestBatchItem { Size = 2 });
    bool full =  batch.GetBatchSize(MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeFalse();
    batch.AddBatchItem(new TestBatchItem { Size = 2 });
    full =  batch.GetBatchSize(MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeFalse();
    batch.AddBatchItem(new TestBatchItem { Size = 2 });
    full =  batch.GetBatchSize(MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeTrue();
  }
  
  [Fact]
  public void Large_Message_Problem_2()
  {
    const int MAX_BATCH_SIZE = 5;
    using var batch = new Batch<TestBatchItem>();
    batch.AddBatchItem(new TestBatchItem { Size = 6 });
    bool full =  batch.GetBatchSize(MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeTrue();
  }
}
