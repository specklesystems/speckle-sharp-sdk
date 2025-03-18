
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Sdk;
using Speckle.Sdk.Api;

var services = new ServiceCollection();
services.AddSpeckleSdk("Initialization-Test", "test", "1.0", assemblies: [typeof(RevitObject).Assembly]);
var serviceProvider = services.BuildServiceProvider();


var url = "https://latest.speckle.systems/"; //small?
var streamId = "a3ac1b2706";
var rootId = "7d53bcf28c6696ecac8781684a0aa006";

var operations = serviceProvider.GetRequiredService<IOperations>();
var _ = await operations.Receive2(new Uri(url), streamId, rootId, null, null, CancellationToken.None);
