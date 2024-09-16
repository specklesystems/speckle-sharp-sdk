using NUnit.Framework;
using Shouldly;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Tests.Unit.Common;

public class NotNullTests
{
  [TestCase(null, 0)]
  [TestCase(new string[0], 0)]
  [TestCase(new[] { "yay" }, 1)]
  public void Empty(string[]? test, int length)
  {
    var list = NotNullExtensions.Empty(test).ToList();
    list.Count.ShouldBe(length);
  }

  [Test]
  public void NotNullClass()
  {
    var t = NotNullExtensions.NotNull("test");
    t.ShouldNotBeNull().ShouldBe("test");
  }

  [Test]
  public void NotNullStruct()
  {
    var t = NotNullExtensions.NotNull<int>(2);
    t.ShouldBe(2);
  }

  [Test]
  public async Task NotNullClass_Task()
  {
    var t = await NotNullExtensions.NotNull(Task.FromResult<string?>("test"));
    t.ShouldNotBeNull().ShouldBe("test");
  }

  [Test]
  public async Task NotNullStruct_Task()
  {
    var t = await NotNullExtensions.NotNull(Task.FromResult<int?>(2));
    t.ShouldBe(2);
  }

  [Test]
  public async Task NotNullClass_ValueTask()
  {
    var t = await NotNullExtensions.NotNull(ValueTask.FromResult<string?>("test"));
    t.ShouldNotBeNull().ShouldBe("test");
  }

  [Test]
  public async Task NotNullStruct_ValueTask()
  {
    var t = await NotNullExtensions.NotNull(ValueTask.FromResult<int?>(2));
    t.ShouldBe(2);
  }

  [Test]
  public void NotNullClass_Exception() =>
    Assert.Throws<ArgumentNullException>(() => NotNullExtensions.NotNull((string?)null));

  [Test]
  public void NotNullStruct_Exception() =>
    Assert.Throws<ArgumentNullException>(() => NotNullExtensions.NotNull((int?)null));

  [Test]
  public void NotNullClass_Task_Exception() =>
    Assert.ThrowsAsync<ArgumentNullException>(() => NotNullExtensions.NotNull(Task.FromResult((string?)null)));

  [Test]
  public void NotNullStruct_Task_Exception() =>
    Assert.ThrowsAsync<ArgumentNullException>(() => NotNullExtensions.NotNull(Task.FromResult((int?)null)));

  [Test]
  public void NotNullClass_ValueTask_Exception() =>
    Assert.ThrowsAsync<ArgumentNullException>(
      async () => await NotNullExtensions.NotNull(ValueTask.FromResult((string?)null))
    );

  [Test]
  public void NotNullStruct_ValueTask_Exception() =>
    Assert.ThrowsAsync<ArgumentNullException>(
      async () => await NotNullExtensions.NotNull(ValueTask.FromResult((int?)null))
    );
}
