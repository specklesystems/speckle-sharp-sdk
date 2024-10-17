using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Tests.Performance;

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
using var dataSource = ActivatorUtilities.CreateInstance<TestDataHelper>(serviceProvider);
await dataSource
  .SeedTransport(new Account() { serverInfo = new() { url = url } }, streamId, rootId)
  .ConfigureAwait(false);

Console.WriteLine("Attach");
Console.ReadLine();
Console.WriteLine("Executing");
using DeserializeProcess sut = new(dataSource.Transport);
var @base = await sut.Deserialize(dataSource.ObjectId).ConfigureAwait(false);
Debug.Assert(@base.id.Equals(rootId));
Console.WriteLine("Detach");
Console.ReadLine();
