using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Initialization;
using Speckle.Sdk.Serialisation.V2.Send;

var services = new ServiceCollection();
services.AddSpeckleSdk("Initialization-Test", "test", "1.0", assemblies: [typeof(RevitObject).Assembly]);
var serviceProvider = services.BuildServiceProvider();

var url = "https://latest.speckle.systems/"; //small?
var streamId = "a3ac1b2706";
var rootId = "7d53bcf28c6696ecac8781684a0aa006";

var operations = serviceProvider.GetRequiredService<IOperations>();
var root = await operations
  .Receive2(new Uri(url), streamId, rootId, null, new Progress(), CancellationToken.None)
  .ConfigureAwait(false);

await operations
  .Send2(
    new Uri(url),
    streamId,
    null,
    root,
    new Progress(),
    CancellationToken.None,
    new SerializeProcessOptions(SkipCacheRead: true, SkipCacheWrite: true, SkipServer: true)
  )
  .ConfigureAwait(false);
