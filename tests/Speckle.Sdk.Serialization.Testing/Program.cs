using System.Reflection;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Send;
using Speckle.Sdk.Serialization.Testing;

TypeLoader.Reset();
TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());

//var url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?

var url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?
using var dataSource = new TestDataHelper();
await dataSource.SeedTransport(new(url)).ConfigureAwait(false);
SpeckleObjectDeserializer deserializer = new() { ReadTransport = dataSource.Transport };
string data = await dataSource.Transport.GetObject(dataSource.ObjectId).NotNull().ConfigureAwait(false);
var testData = await deserializer.DeserializeJsonAsync(data).ConfigureAwait(false);

Console.WriteLine("Attach");
Console.ReadLine();
Console.WriteLine("Executing");
SpeckleObjectSerializer2 sut = new(SpeckleObjectSerializer2Pool.Instance);
var x = sut.Serialize(testData);
Console.WriteLine("Detach");
Console.ReadLine();
