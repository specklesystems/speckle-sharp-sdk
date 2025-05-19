using FluentAssertions;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Unit.Common;

public class NotNullTests
{
  [Theory]
  [InlineData(null, 0)]
  [InlineData(new string[0], 0)]
  [InlineData(new[] { "yay" }, 1)]
  public void Empty(string[]? test, int length)
  {
    var list = test.Empty().ToList();
    list.Count.Should().Be(length);
  }

  [Fact]
  public void NotNullClass()
  {
    var t = "test".NotNull();
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
    var t = await Task.FromResult<string?>("test").NotNull();
    t.Should().Be("test");
  }

  [Fact]
  public async Task NotNullStruct_Task()
  {
    var t = await Task.FromResult<int?>(2).NotNull();
    t.Should().Be(2);
  }

  [Fact]
  public async Task NotNullClass_ValueTask()
  {
    var t = await ValueTask.FromResult<string?>("test").NotNull();
    t.Should().Be("test");
  }

  [Fact]
  public async Task NotNullStruct_ValueTask()
  {
    var t = await ValueTask.FromResult<int?>(2).NotNull();
    t.Should().Be(2);
  }

  [Fact]
  public void NotNullClass_Exception() => FluentActions.Invoking(() => ((string?)null).NotNull());

  [Fact]
  public void NotNullStruct_Exception() => FluentActions.Invoking(() => ((int?)null).NotNull());

  [Fact]
  public async Task NotNullClass_Task_Exception() =>
    await FluentActions
      .Invoking(async () => await Task.FromResult((string?)null).NotNull())
      .Should()
      .ThrowAsync<ArgumentNullException>();

  [Fact]
  public async Task NotNullStruct_Task_Exception() =>
    await FluentActions
      .Invoking(async () => await Task.FromResult((int?)null).NotNull())
      .Should()
      .ThrowAsync<ArgumentNullException>();

  [Fact]
  public async Task NotNullClass_ValueTask_Exception() =>
    await FluentActions
      .Invoking(async () => await ValueTask.FromResult((string?)null).NotNull())
      .Should()
      .ThrowAsync<ArgumentNullException>();

  [Fact]
  public async Task NotNullStruct_ValueTask_Exception() =>
    await FluentActions
      .Invoking(async () => await ValueTask.FromResult((int?)null).NotNull())
      .Should()
      .ThrowAsync<ArgumentNullException>();

  [Theory]
  [InlineData("hello")]
  [InlineData("  world  ")]
  public void NotNullOrWhiteSpace_Valid(string input)
  {
    var result = input.NotNullOrWhiteSpace();
    result.Should().Be(input);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void NotNullOrWhiteSpace_Invalid(string? input)
  {
    Action act = () => input.NotNullOrWhiteSpace();
    if (input is null)
    {
      act.Should().Throw<ArgumentNullException>();
    }
    else
    {
      act.Should().Throw<ArgumentException>();
    }
  }

  [Theory]
  [InlineData("foo")]
  [InlineData("bar baz")]
  public void ValidateNullOrWhiteSpace_Valid(string input)
  {
    input.ValidateNullOrWhiteSpace(); // Should not throw
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void ValidateNullOrWhiteSpace_Invalid(string input)
  {
    Action act = () => input.ValidateNullOrWhiteSpace();
    act.Should().Throw<ArgumentException>();
  }
}
