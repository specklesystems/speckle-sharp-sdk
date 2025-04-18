#pragma warning disable CA1506
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Serialization.Testing;

const bool skipCacheReceive = false;
const bool skipCacheSendCheck = true;
const bool skipCacheSendSave = false;

var url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?
var streamId = "a3ac1b2706";
var rootId = "7d53bcf28c6696ecac8781684a0aa006";

/*
var url = "https://latest.speckle.systems/"; //other?
var streamId = "368f598929";
var rootId = "67374cfe689c43ff8be12090af122244";*/

/*
var url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?
var streamId = "2099ac4b5f";
var rootId = "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6";*/

var serviceCollection = new ServiceCollection();
serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3");
var serviceProvider = serviceCollection.BuildServiceProvider();

Console.WriteLine("Attach");

var token = serviceProvider.GetRequiredService<IAccountManager>().GetDefaultAccount()?.token;
var progress = new Progress(true);

var factory = serviceProvider.GetRequiredService<IDeserializeProcessFactory>();
var process = factory.CreateDeserializeProcess(new Uri(url), streamId, token, progress, default, new(skipCacheReceive));
var @base = await process.Deserialize(rootId).ConfigureAwait(false);
Console.WriteLine("Deserialized");
Console.ReadLine();
Console.WriteLine("Executing");

var serializeProcessFactory = serviceProvider.GetRequiredService<ISerializeProcessFactory>();
var serializeProcess = serializeProcessFactory.CreateSerializeProcess(
  new Uri(url),
  streamId,
  token,
  progress,
  default,
  new SerializeProcessOptions(skipCacheSendCheck, skipCacheSendSave, true, true)
);
await serializeProcess.Serialize(@base).ConfigureAwait(false);
Console.WriteLine("Detach");
Console.ReadLine();
await serializeProcess.DisposeAsync().ConfigureAwait(false);
#pragma warning restore CA1506
