using System.Reflection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

TypeLoader.Reset();
TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());

  var factory = new ServerTransportFactory();
  StreamWrapper sw = new("https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6");//""https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e");
  var acc = await sw.GetAccount().ConfigureAwait(false);
  using var client = new Client(acc);
  var branch = await client.BranchGet(sw.StreamId, sw.BranchName!, 1).ConfigureAwait(false);
  var objectId = branch.commits.items[0].referencedObject;

  using ServerTransport remoteTransport = new(acc, sw.StreamId);
  using var transport = factory.Create(acc, sw.StreamId);
     
     
  var stage = new ReceiveStage(transport);
  var @base = await stage.GetObject(objectId).ConfigureAwait(false);

  Console.WriteLine(@base.id == objectId);
