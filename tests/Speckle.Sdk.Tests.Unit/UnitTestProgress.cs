
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Speckle.Sdk.Tests.Unit;

public class UnitTestProgress<T>(Action<T> handler) : IProgress<T>
{
  public void Report(T value) => handler(value);
}
