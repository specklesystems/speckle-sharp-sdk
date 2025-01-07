using FluentAssertions;
using Speckle.Sdk.Common;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Common;

public class NotNullTests
{
  [Theory]
  [InlineData(null, 0)]
  [InlineData(new string[0], 0)]
  [InlineData(new[] { "yay" }, 1)]
  public void Empty(string[]? test, int length)
  {
    var list = NotNullExtensions.Empty(test).ToList();
    list.Count.Should().Be(length);
  }

  [Fact]
  public void NotNullClass()
  {
    var t = NotNullExtensions.NotNull("test");
    t.Should().Be("test");
  }

  [Fact]
  public void NotNullStruct()
  {
    var t = NotNullExtensions.NotNull<int>(2);
    t.Should().Be(2);
  }

  [Fact]
  public async Task NotNullClass_Task()
  {
    var t = await NotNullExtensions.NotNull(Task.FromResult<string?>("test"));
    t.Should().Be("test");
  }

  [Fact]
  public async Task NotNullStruct_Task()
  {
    var t = await NotNullExtensions.NotNull(Task.FromResult<int?>(2));
    t.Should().Be(2);
  }

  [Fact]
  public async Task NotNullClass_ValueTask()
  {
    var t = await NotNullExtensions.NotNull(ValueTask.FromResult<string?>("test"));
    t.Should().Be("test");
  }

  [Fact]
  public async Task NotNullStruct_ValueTask()
  {
    var t = await NotNullExtensions.NotNull(ValueTask.FromResult<int?>(2));
    t.Should().Be(2);
  }

  [Fact]
  public void NotNullClass_Exception()
  {
    FluentActions.Invoking(() => NotNullExtensions.NotNull((string?)null));
  }

  [Fact]
  public void NotNullStruct_Exception()
  {
    FluentActions.Invoking(() => NotNullExtensions.NotNull((int?)null));
  }

  [Fact]
  public async Task NotNullClass_Task_Exception()
  {
    await FluentActions
      .Invoking(async () => await NotNullExtensions.NotNull(Task.FromResult((string?)null)))
      .Should()
      .ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task NotNullStruct_Task_Exception()
  {
    await FluentActions
      .Invoking(async () => await NotNullExtensions.NotNull(Task.FromResult((int?)null)))
      .Should()
      .ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task NotNullClass_ValueTask_Exception()
  {
    await FluentActions
      .Invoking(async () => await NotNullExtensions.NotNull(ValueTask.FromResult((string?)null)))
      .Should()
      .ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task NotNullStruct_ValueTask_Exception()
  {
    await FluentActions
      .Invoking(async () => await NotNullExtensions.NotNull(ValueTask.FromResult((int?)null)))
      .Should()
      .ThrowAsync<ArgumentNullException>();
  }
}
