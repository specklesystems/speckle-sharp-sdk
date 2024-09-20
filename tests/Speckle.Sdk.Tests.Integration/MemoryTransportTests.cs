using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Integration;

public class MemoryTransportTests
{
  private readonly MemoryTransport _memoryTransport = new(blobStorageEnabled: true);
  private IOperations _operations;

  [SetUp]
  public void Setup()
  {
    CleanData();
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [TearDown]
  public void TearDown() => CleanData();

  private void CleanData()
  {
    if (Directory.Exists(_memoryTransport.BlobStorageFolder))
    {
      Directory.Delete(_memoryTransport.BlobStorageFolder, true);
    }
    Directory.CreateDirectory(_memoryTransport.BlobStorageFolder);
  }

  [Test]
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
    Assert.That(blobPaths, Has.Count.EqualTo(3));
    var objectBlobs = receivedObject["blobs"] as IList<object>;
    objectBlobs.ShouldNotBeNull();
    var blobs = objectBlobs.Cast<Blob>().ToList();
    // Check that we have three blobs
    Assert.That(blobs, Has.Count.EqualTo(3));
    // Check that received blobs point to local path (where they were received)
    Assert.That(blobs[0].filePath, Contains.Substring(_memoryTransport.BlobStorageFolder));
    Assert.That(blobs[1].filePath, Contains.Substring(_memoryTransport.BlobStorageFolder));
    Assert.That(blobs[2].filePath, Contains.Substring(_memoryTransport.BlobStorageFolder));
  }
}
