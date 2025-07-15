using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Speckle.Sdk.Caching;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Tests.Unit;

public class ModelCacheManagerMockTests : MoqTest
{
  private readonly Mock<IFileSystem> _fileSystemMock;
  private readonly ModelCacheManager _manager;

  public ModelCacheManagerMockTests()
  {
    Mock<ILogger<ModelCacheManager>> loggerMock = Create<ILogger<ModelCacheManager>>(MockBehavior.Loose);
    _fileSystemMock = Create<IFileSystem>();
    _manager = new ModelCacheManager(loggerMock.Object, _fileSystemMock.Object);
  }

  [Fact]
  public void ClearCache_ShouldNotDeleteFiles_WhenDirectoryDoesNotExist()
  {
    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
    _manager.ClearCache();
    _fileSystemMock.Verify(fs => fs.EnumerateFiles(It.IsAny<string>()), Times.Never);
    _fileSystemMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public void ClearCache_ShouldDeleteFiles_WhenDirectoryExists()
  {
    var files = new List<string> { "file1.db", "file2.db" };
    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
    _fileSystemMock.Setup(fs => fs.EnumerateFiles(It.IsAny<string>())).Returns(files);
    foreach (var file in files)
    {
      _fileSystemMock.Setup(fs => fs.DeleteFile(file));
    }
    _manager.ClearCache();
  }

  [Fact]
  public void ClearCache_ShouldLogWarning_WhenDeleteFileThrows()
  {
    var files = new List<string> { "file1.db" };
    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
    _fileSystemMock.Setup(fs => fs.EnumerateFiles(It.IsAny<string>())).Returns(files);
    _fileSystemMock.Setup(fs => fs.DeleteFile(It.IsAny<string>())).Throws<IOException>();
    _manager.ClearCache();
  }

  [Fact]
  public void GetCacheSize_ShouldReturnZero_WhenDirectoryDoesNotExist()
  {
    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);
    var size = _manager.GetCacheSize();
    size.Should().Be(0);
  }

  [Fact]
  public void GetCacheSize_ShouldSumFileSizes()
  {
    var files = new List<string> { "file1.db", "file2.db" };
    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
    _fileSystemMock.Setup(fs => fs.EnumerateFiles(It.IsAny<string>())).Returns(files);
    _fileSystemMock.Setup(fs => fs.GetFileSize("file1.db")).Returns(10);
    _fileSystemMock.Setup(fs => fs.GetFileSize("file2.db")).Returns(20);
    var size = _manager.GetCacheSize();
    size.Should().Be(30);
  }

  [Fact]
  public void GetCacheSize_ShouldLogWarning_WhenGetFileSizeThrows()
  {
    var files = new List<string> { "file1.db" };
    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
    _fileSystemMock.Setup(fs => fs.EnumerateFiles(It.IsAny<string>())).Returns(files);
    _fileSystemMock.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Throws<IOException>();
    var size = _manager.GetCacheSize();
    size.Should().Be(0);
  }
}
