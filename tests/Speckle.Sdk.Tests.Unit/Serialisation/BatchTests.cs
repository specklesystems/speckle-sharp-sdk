using FluentAssertions;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class BatchTests
{
  private class BatchItem(int size) : IHasByteSize
  {
    public int ByteSize { get; } = size;
  }

  private static readonly Action<string> EMPTY_LOGGER = _ => { };

  [Fact]
  public void TestBatchSize_Calc()
  {
    using var batch = new Batch<BatchItem>();
    batch.Add(new BatchItem(1));
    batch.BatchByteSize.Should().Be(1);
    batch.Add(new BatchItem(2));
    batch.BatchByteSize.Should().Be(3);
  }

  [Fact]
  public void Ensure_logging()
  {
    using var batch = new Batch<BatchItem>();
    batch.AddBatchItem(new BatchItem(2));
    bool called = false;
    batch.GetBatchSize(x => called = true, 1);
    called.Should().BeTrue();
  }

  [Fact]
  public void TestBatchSize_Trim()
  {
    using var batch = new Batch<BatchItem>();
    batch.Add(new BatchItem(1));
    batch.Add(new BatchItem(2));
    batch.BatchByteSize.Should().Be(3);

    batch.Items.Capacity.Should().Be(Pools.DefaultCapacity);
    batch.TrimExcess();

    batch.Items.Capacity.Should().Be(2);
    batch.BatchByteSize.Should().Be(3);
  }

  [Fact]
  public void Basics()
  {
    using var batch = BatchExtensions.CreateBatch<BatchItem>();
    batch.AddBatchItem(new BatchItem(2));
    batch.BatchByteSize.Should().Be(2);
    batch.AddBatchItem(new BatchItem(2));
    batch.BatchByteSize.Should().Be(4);
    batch.AddBatchItem(new BatchItem(2));
    batch.BatchByteSize.Should().Be(6);

    batch.TrimExcess();
    batch.BatchByteSize.Should().Be(6);
    batch.Items.Count.Should().Be(3);

    batch.AddBatchItem(new BatchItem(2));
    batch.BatchByteSize.Should().Be(8);
    batch.Items.Count.Should().Be(4);
  }

  [Fact]
  public void Large_Message_Problem_1()
  {
    const int MAX_BATCH_SIZE = 5;

    using var batch = BatchExtensions.CreateBatch<BatchItem>();
    batch.AddBatchItem(new BatchItem(2));
    bool full = batch.GetBatchSize(EMPTY_LOGGER, MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeFalse();
    batch.AddBatchItem(new BatchItem(2));
    full = batch.GetBatchSize(EMPTY_LOGGER, MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeFalse();
    batch.AddBatchItem(new BatchItem(2));
    full = batch.GetBatchSize(EMPTY_LOGGER, MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeTrue();
  }

  [Fact]
  public void Large_Message_Problem_2()
  {
    const int MAX_BATCH_SIZE = 5;

    using var batch = BatchExtensions.CreateBatch<BatchItem>();
    batch.AddBatchItem(new BatchItem(63));
    bool full = batch.GetBatchSize(EMPTY_LOGGER, MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeTrue();
  }

  [Fact]
  public void Large_Message_Problem_3()
  {
    const int MAX_BATCH_SIZE = 5;

    using var batch = BatchExtensions.CreateBatch<BatchItem>();
    batch.AddBatchItem(new BatchItem(2));
    bool full = batch.GetBatchSize(EMPTY_LOGGER, MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeFalse();
    batch.AddBatchItem(new BatchItem(63));
    full = batch.GetBatchSize(EMPTY_LOGGER, MAX_BATCH_SIZE) == MAX_BATCH_SIZE;
    full.Should().BeTrue();
  }
}
