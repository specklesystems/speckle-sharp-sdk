using System.Collections.Concurrent;
using System.Reflection;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Integration;

public class MemoryTransportTests
{  private MemoryTransport _memoryTransport;

  [SetUp]
  public void Setup()
  {
    _memoryTransport = new MemoryTransport(new ConcurrentDictionary<string, string>());
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
  }
  [Test]
  public async Task SendAndReceiveObjectWithBlobs()
  {
    var myObject = Fixtures.GenerateSimpleObject();
    myObject["@blobs"] = Fixtures.GenerateThreeBlobs();

    var sendResult = await Operations.Send(myObject, _memoryTransport, false);

    // NOTE: used to debug diffing
    // await Operations.Send(myObject, new List<ITransport> { transport });

    var receivedObject = await Operations.Receive(sendResult.rootObjId, _memoryTransport, new MemoryTransport());

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
