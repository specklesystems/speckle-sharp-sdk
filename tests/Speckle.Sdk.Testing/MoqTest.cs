using System.Diagnostics.CodeAnalysis;
using Moq;

namespace Speckle.Sdk.Testing;

[ExcludeFromCodeCoverage]
public abstract class MoqTest : IDisposable
{
  protected MoqTest() => Repository = new(MockBehavior.Strict);

  protected virtual void Dispose(bool isDisposing)
  {
    if (isDisposing)
    {
      Repository.VerifyAll();
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected MockRepository Repository { get; private set; } = new(MockBehavior.Strict);

  protected Mock<T> Create<T>(MockBehavior behavior = MockBehavior.Strict)
    where T : class => Repository.Create<T>(behavior);
}
