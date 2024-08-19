using NUnit.Framework;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Objects.Tests.Unit;

public class LogSendTest
{
  private const string OBJECT_ID = "5cbf84a0061172102ef8a66ae914f232";
  private static readonly Uri s_modelUrl = new("https://myspeckle.server.example/projects/asf12354/models/hgfd456656");

  [Test]
  public async Task SendTest()
  {
    var testData = await GetSampleData(OBJECT_ID);

    SpeckleLog.Logger.Information("Starting Long Send Test Send");

    var destinationTransport = await GetDestination();

    var res = await Operations.Send(testData, new[] { destinationTransport });

    SpeckleLog.Logger.Information("Starting Send was successful: {objectId}", res);
  }

  private static async Task<ITransport> GetDestination()
  {
    StreamWrapper sw = new(s_modelUrl.ToString());
    var acc = await sw.GetAccount();
    return new ServerTransport(acc, sw.StreamId);
  }

  private static async Task<Base> GetSampleData(string objectId)
  {
    SpeckleLog.Logger.Information("Gathering Sample Data Set");

    using SQLiteTransport source = new(SpecklePathProvider.UserApplicationDataPath(), "longsendtest");
    MemoryTransport memoryTransport = new();
    return await Operations.Receive(objectId, source, memoryTransport);
  }
}
