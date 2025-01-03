using Shouldly;
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
    list.Count.ShouldBe(length);
  }

  [Fact]
  public void NotNullClass()
  {
    var t = NotNullExtensions.NotNull("test");
    t.ShouldNotBeNull().ShouldBe("test");
  }

  [Fact]
  public void NotNullStruct()
  {
    var t = NotNullExtensions.NotNull<int>(2);
    t.ShouldBe(2);
  }

  [Fact]
  public async Task NotNullClass_Task()
  {
    var t = await NotNullExtensions.NotNull(Task.FromResult<string?>("test"));
    t.ShouldNotBeNull().ShouldBe("test");
  }

  [Fact]
  public async Task NotNullStruct_Task()
  {
    var t = await NotNullExtensions.NotNull(Task.FromResult<int?>(2));
    t.ShouldBe(2);
  }

  [Fact]
  public async Task NotNullClass_ValueTask()
  {
    var t = await NotNullExtensions.NotNull(ValueTask.FromResult<string?>("test"));
    t.ShouldNotBeNull().ShouldBe("test");
  }

  [Fact]
  public async Task NotNullStruct_ValueTask()
  {
    var t = await NotNullExtensions.NotNull(ValueTask.FromResult<int?>(2));
    t.ShouldBe(2);
  }

  [Fact]
  public void NotNullClass_Exception()
  {
    var exception = Should.Throw<ArgumentNullException>(() =>
      NotNullExtensions.NotNull((string?)null)
    );
    exception.ShouldNotBeNull();
  }

  [Fact]
  public void NotNullStruct_Exception()
  {
    var exception = Should.Throw<ArgumentNullException>(() =>
      NotNullExtensions.NotNull((int?)null)
    );
    exception.ShouldNotBeNull();
  }

  [Fact]
  public void NotNullClass_Task_Exception()
  {
    var exception = Should.ThrowAsync<ArgumentNullException>(async () =>
      await NotNullExtensions.NotNull(Task.FromResult((string?)null))
    );
    exception.ShouldNotBeNull();
  }

  [Fact]
  public void NotNullStruct_Task_Exception()
  {
    var exception = Should.ThrowAsync<ArgumentNullException>(async () =>
      await NotNullExtensions.NotNull(Task.FromResult((int?)null))
    );
    exception.ShouldNotBeNull();
  }

  [Fact]
  public void NotNullClass_ValueTask_Exception()
  {
    var exception = Should.ThrowAsync<ArgumentNullException>(async () =>
      await NotNullExtensions.NotNull(ValueTask.FromResult((string?)null))
    );
    exception.ShouldNotBeNull();
  }

  [Fact]
  public void NotNullStruct_ValueTask_Exception()
  {
    var exception = Should.ThrowAsync<ArgumentNullException>(async () =>
      await NotNullExtensions.NotNull(ValueTask.FromResult((int?)null))
    );
    exception.ShouldNotBeNull();
  }
}
