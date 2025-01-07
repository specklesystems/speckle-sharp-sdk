using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Integration;

public class MemoryTransportTests : IDisposable
{
  private readonly MemoryTransport _memoryTransport = new(blobStorageEnabled: true);
  private IOperations _operations;

  public MemoryTransportTests()
  {
    CleanData();
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  public void Dispose() => CleanData();

  private void CleanData()
  {
    if (Directory.Exists(_memoryTransport.BlobStorageFolder))
    {
      Directory.Delete(_memoryTransport.BlobStorageFolder, true);
    }

    Directory.CreateDirectory(_memoryTransport.BlobStorageFolder);
  }

  [Fact]
  public async Task SendAndReceiveObjectWithBlobs()
  {
    var myObject = Fixtures.GenerateSimpleObject();
    myObject["blobs"] = Fixtures.GenerateThreeBlobs();

    var sendResult = await _operations.Send(myObject, _memoryTransport, false);

    // NOTE: used to debug diffing
    // await Operations.Send(myObject, new List<ITransport> { transport });

    var receivedObject = await _operations.Receive(sendResult.rootObjId, _memoryTransport, new MemoryTransport());

    var allFiles = Directory
      .GetFiles(_memoryTransport.BlobStorageFolder)
      .Select(fp => fp.Split(Path.DirectorySeparatorChar).Last())
      .ToList();

    var blobPaths = allFiles
      .Where(fp => fp.Length > Blob.LocalHashPrefixLength) // excludes things like .DS_store
      .ToList();

    // Check that there are three downloaded blobs!
    blobPaths.Count.Should().Be(3);

    var objectBlobs = receivedObject["blobs"] as IList<object>;
    objectBlobs.Should().NotBeNull();

    var blobs = objectBlobs!.Cast<Blob>().ToList();
    // Check that we have three blobs
    blobs.Count.Should().Be(3);

    // Check that received blobs point to local path (where they were received)
    blobs[0].filePath.Should().Contain(_memoryTransport.BlobStorageFolder);
    blobs[1].filePath.Should().Contain(_memoryTransport.BlobStorageFolder);
    blobs[2].filePath.Should().Contain(_memoryTransport.BlobStorageFolder);
  }
}
