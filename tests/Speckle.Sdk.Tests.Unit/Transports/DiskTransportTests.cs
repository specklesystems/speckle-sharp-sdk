using System;
using System.IO;
using FluentAssertions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Transports;

public sealed class DiskTransportTests : TransportTests, IDisposable
{
  private readonly DiskTransport _diskTransport;
  private readonly string _basePath = $"./temp_{Guid.NewGuid()}";
  private const string ApplicationName = "Speckle Integration Tests";
  private readonly string _fullPath;

  protected override ITransport Sut => _diskTransport.NotNull();

  public DiskTransportTests()
  {
    _fullPath = Path.Combine(_basePath, ApplicationName);
    _diskTransport = new DiskTransport(_fullPath);
  }

  [Fact]
  public void DirectoryCreated_AfterInitialization()
  {
    // Act
    var directoryExists = Directory.Exists(_fullPath);

    // Assert
    directoryExists.Should().BeTrue();
  }

  public void Dispose()
  {
    if (Directory.Exists(_basePath))
    {
      Directory.Delete(_basePath, true);
    }
  }
}
