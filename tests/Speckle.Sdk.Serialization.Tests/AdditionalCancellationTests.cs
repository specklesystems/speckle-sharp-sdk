using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;

namespace Speckle.Sdk.Serialization.Tests;

public class AdditionalCancellationTests
{
  private readonly ISerializeProcessFactory _factory;

  public AdditionalCancellationTests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(TestClass).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _factory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
  }

  [Fact]
  public async Task Cancellation_Traversal()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var cancellationSource = new CancellationTokenSource();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      new ConcurrentDictionary<string, string>(),
      null,
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
    );

    await cancellationSource.CancelAsync();
    var task = serializeProcess.Serialize(testClass);

    var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
    await Verify(ex);
    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }

  [Fact]
  public async Task Cancellation_MultipleConcurrent()
  {
    var testClass1 = new TestClass() { RegularProperty = "Hello" };
    var testClass2 = new TestClass() { RegularProperty = "World" };

    using var cancellationSource = new CancellationTokenSource();

    await cancellationSource.CancelAsync();
    var tasks = new List<Task>();
    for (int i = 0; i < 2; i++)
    {
      var serializeProcess = _factory.CreateSerializeProcess(
        new ConcurrentDictionary<Id, Json>(),
        new ConcurrentDictionary<string, string>(),
        null,
        cancellationSource.Token,
        new SerializeProcessOptions(true, true, false, true)
      );
      tasks.Add(serializeProcess.Serialize(i % 2 == 0 ? testClass1 : testClass2));
    }

    while (tasks.Count != 0)
    {
      await Assert.ThrowsAsync<OperationCanceledException>(async () =>
      {
        var t = await Task.WhenAny(tasks);
        tasks.Remove(t);
        await t;
      });
    }
    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }

  [Fact]
  public async Task Cancellation_AfterCompletion()
  {
    var testClass = new TestClass() { RegularProperty = "Hello" };

    using var cancellationSource = new CancellationTokenSource();

    await using var serializeProcess = _factory.CreateSerializeProcess(
      new ConcurrentDictionary<Id, Json>(),
      new ConcurrentDictionary<string, string>(),
      null,
      cancellationSource.Token,
      new SerializeProcessOptions(true, true, false, true)
    );

    await serializeProcess.Serialize(testClass);
    await cancellationSource.CancelAsync();

    cancellationSource.IsCancellationRequested.Should().BeTrue();
  }
}
