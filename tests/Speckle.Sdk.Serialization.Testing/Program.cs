using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Serialization.Testing;

const bool skipCache = false;
TypeLoader.Reset();
TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());

/*
var url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?
var streamId = "a3ac1b2706";
var rootId = "7d53bcf28c6696ecac8781684a0aa006";*/


var url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?
var streamId = "2099ac4b5f";
var rootId = "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6";

var serviceCollection = new ServiceCollection();
serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
var serviceProvider = serviceCollection.BuildServiceProvider();

Console.WriteLine("Attach");
Console.ReadLine();
Console.WriteLine("Executing");

var progress = new Progress(true);
var sqliteTransport = new SQLiteCacheManager(streamId);
var serverObjects = new ServerObjectManager(
  serviceProvider.GetRequiredService<ISpeckleHttp>(),
  serviceProvider.GetRequiredService<ISdkActivityFactory>(),
  new Uri(url),
  null
);
var o = new ObjectLoader(sqliteTransport, serverObjects, streamId, progress);
var process = new DeserializeProcess(progress, o);
await process.Deserialize(rootId, default, new(skipCache)).ConfigureAwait(false);
Console.WriteLine("Detach");
Console.ReadLine();
