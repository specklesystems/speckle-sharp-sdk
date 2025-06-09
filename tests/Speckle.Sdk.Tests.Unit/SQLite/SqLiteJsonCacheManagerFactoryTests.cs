using FluentAssertions;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Tests.Unit.SQLite;

public class SqLiteJsonCacheManagerFactoryTests
{
  [Fact]
  public void CreateForUser_ShouldReturnManagerWithCorrectPathAndConcurrency()
  {
    // Arrange
    var scope = "testuser";
    var expectedPath = Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db");

    // Act
    using (var factory = new SqLiteJsonCacheManagerFactory())
    {
      using (var manager = factory.CreateForUser(scope))
      {
        // Assert
        manager.Should().NotBeNull();
        manager.Pool.Path.Should().Be(expectedPath);
        manager.Pool.Concurrency.Should().Be(1);
      }
    }

    // Cleanup
    if (File.Exists(expectedPath))
    {
      File.Delete(expectedPath);
    }
  }

  [Fact]
  public void CreateFromStream_ShouldReturnManagerWithCorrectPathAndConcurrency_AndCleanup()
  {
    // Arrange
    var streamId = "stream123";
    var expectedPath = SqlitePaths.GetDBPath(streamId);
    using (var factory = new SqLiteJsonCacheManagerFactory())
    {
      // Act
      using (var manager = factory.CreateFromStream(streamId))
      {
        // Assert
        manager.Should().NotBeNull();
        manager.Pool.Path.Should().Be(expectedPath);
        manager.Pool.Concurrency.Should().Be(SqLiteJsonCacheManagerFactory.INITIAL_CONCURRENCY);
      }
    }

    // Cleanup
    if (File.Exists(expectedPath))
    {
      File.Delete(expectedPath);
    }
  }

  [Fact]
  public void CreateFromStream_ShouldReturnCachedManagerForSameStreamId_AndCleanup()
  {
    var streamId = "stream123";
    var expectedPath = SqlitePaths.GetDBPath(streamId);
    // Arrange
    using (var factory = new SqLiteJsonCacheManagerFactory())
    {
      // Act
      using (var manager1 = factory.CreateFromStream(streamId))
      {
        using var manager2 = factory.CreateFromStream(streamId);

        // Assert
        manager1.Should().NotBeSameAs(manager2);
        manager1.Pool.Should().BeSameAs(manager2.Pool);
      }
    }

    // Cleanup
    if (File.Exists(expectedPath))
    {
      File.Delete(expectedPath);
    }
  }
}
