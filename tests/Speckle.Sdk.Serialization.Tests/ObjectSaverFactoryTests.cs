using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Serialisation.V2.Send.Tests;

public class ObjectSaverFactoryTests : MoqTest
{
  private readonly Mock<ILoggerFactory> _loggerFactoryMock;
  private readonly Mock<ILogger<ObjectSaver>> _loggerMock;
  private readonly ObjectSaverFactory _factory;

  public ObjectSaverFactoryTests()
  {
    _loggerFactoryMock = Create<ILoggerFactory>();
    _loggerMock = Create<ILogger<ObjectSaver>>();
    _factory = new ObjectSaverFactory(_loggerFactoryMock.Object);
  }

  public override void Dispose()
  {
    _factory.Dispose();
    base.Dispose();
  }

  [Fact]
  public void Create_ShouldReturnObjectSaverInstance()
  {
    _loggerFactoryMock.Setup(f => f.CreateLogger(typeof(ObjectSaver).FullName)).Returns(_loggerMock.Object);
    var cacheManagerMock = Create<ISqLiteJsonCacheManager>();
    cacheManagerMock.Setup(x => x.Dispose());
    cacheManagerMock.SetupGet(c => c.Path).Returns("/tmp/test1.db");

    var saver = _factory.Create(
      Create<IServerObjectManager>().Object,
      cacheManagerMock.Object,
      null,
      CancellationToken.None
    );

    saver.Should().NotBeNull();
  }

  [Fact]
  public void Create_ShouldReturnSameInstanceForSamePath()
  {
    _loggerFactoryMock.Setup(f => f.CreateLogger(typeof(ObjectSaver).FullName)).Returns(_loggerMock.Object);
    var cacheManagerMock = Create<ISqLiteJsonCacheManager>();
    cacheManagerMock.Setup(x => x.Dispose());
    cacheManagerMock.SetupGet(c => c.Path).Returns("/tmp/test2.db");

    var saver1 = _factory.Create(
      Create<IServerObjectManager>().Object,
      cacheManagerMock.Object,
      null,
      CancellationToken.None
    );
    var saver2 = _factory.Create(
      Create<IServerObjectManager>().Object,
      cacheManagerMock.Object,
      null,
      CancellationToken.None
    );

    saver1.Should().BeSameAs(saver2);
  }

  [Fact]
  public void Dispose_ShouldDisposeAllSavers()
  {
    var saverMock1 = Create<IObjectSaver>();
    _factory
      .GetType()
      .GetField("_savers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
      ?.SetValue(
        _factory,
        new System.Collections.Concurrent.ConcurrentDictionary<string, IObjectSaver>(
          new[]
          {
            new System.Collections.Generic.KeyValuePair<string, IObjectSaver>("/tmp/test3.db", saverMock1.Object),
          }
        )
      );
    saverMock1.Setup(x => x.Dispose());

    _factory.Dispose();
  }
}
