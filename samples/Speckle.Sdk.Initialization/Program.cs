using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Sdk;
using Speckle.Sdk.Api;

var url = "https://latest.speckle.systems/"; //small?
var streamId = "a3ac1b2706";
var rootId = "7d53bcf28c6696ecac8781684a0aa006";

var services = new ServiceCollection();
services.AddSpeckleSdk("Initialization-Test", "test", "1.0", assemblies: [typeof(RevitObject).Assembly]);
var serviceProvider = services.BuildServiceProvider();

var speckle = serviceProvider.GetSpeckle();
var speckleClient = await speckle.Create(new Uri(url), null);

var root = await speckleClient.Receive(streamId, rootId, CancellationToken.None).ConfigureAwait(false);

await speckleClient
  .Send(
    streamId,
    root,
    CancellationToken.None
  // new SerializeProcessOptions(SkipCacheRead: true, SkipCacheWrite: true, SkipServer: true)
  )
  .ConfigureAwait(false);
