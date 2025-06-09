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
    var factory = new SqLiteJsonCacheManagerFactory();
    var scope = "testuser";
    var expectedPath = Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db");

    // Act
    using (var manager = factory.CreateForUser(scope))
    {
      // Assert
      manager.Should().NotBeNull();
      manager.Path.Should().Be(expectedPath);
      manager.Concurrency.Should().Be(1);
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
    var factory = new SqLiteJsonCacheManagerFactory();
    var streamId = "stream123";
    var expectedPath = SqlitePaths.GetDBPath(streamId);

    // Act
    using (var manager = factory.CreateFromStream(streamId))
    {
      // Assert
      manager.Should().NotBeNull();
      manager.Path.Should().Be(expectedPath);
      manager.Concurrency.Should().Be(SqLiteJsonCacheManagerFactory.INITIAL_CONCURRENCY);
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
    // Arrange
    var factory = new SqLiteJsonCacheManagerFactory();
    var streamId = "stream123";
    var expectedPath = SqlitePaths.GetDBPath(streamId);

    // Act
    using (var manager1 = factory.CreateFromStream(streamId))
    {
      using var manager2 = factory.CreateFromStream(streamId);

      // Assert
      manager1.Should().BeSameAs(manager2);
    }

    // Cleanup
    if (File.Exists(expectedPath))
    {
      File.Delete(expectedPath);
    }
  }
}
