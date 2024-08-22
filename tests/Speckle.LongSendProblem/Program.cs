using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Microsoft.Extensions.DependencyInjection;

//Replace this with a brand new model URL
Uri modelUrl = new("https://testing1.speckle.dev/projects/cdedc63e6d/models/2d68380f1d");

//Keep this as-is, copy the Data.db into %appdata%/longsendtest/Data.db
const string OBJECT_ID = "5cbf84a0061172102ef8a66ae914f232";

SetupSpeckle();
var testData = await GetSampleData(OBJECT_ID);
await SendToSpeckle(testData, modelUrl);

return;

static async Task SendToSpeckle(Base testData, Uri modelUrl)
{
  SpeckleLog.Logger.Information("Starting Long Send Test Send");
  var destinationTransport = await GetDestination(modelUrl);

  var (res, _) = await Operations.Send(testData, new[] { destinationTransport });
  SpeckleLog.Logger.Information("Starting Send was successful: {objectId}", res);
}

static async Task<ITransport> GetDestination(Uri modelUrl)
{
  StreamWrapper sw = new(modelUrl.ToString());
  var acc = await sw.GetAccount();
  return new ServerTransport(acc, sw.StreamId);
}

static async Task<Base> GetSampleData(string objectId)
{
  SpeckleLog.Logger.Information("Gathering Sample Data Set");
  using SQLiteTransport source = new(SpecklePathProvider.UserApplicationDataPath(), "longsendtest");
  MemoryTransport memoryTransport = new();
  return await Operations.Receive(objectId, source, memoryTransport);
}

static void SetupSpeckle()
{
  TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
  var config = new SpeckleConfiguration(
    new("Long Send Problem Test Script", "LongSend"),
    default,
    new(
      MinimumLevel: SpeckleLogLevel.Debug,
      Console: true,
      File: new(Path: "SpeckleCoreLog.txt"),
      Otel: new(
        Endpoint: "https://seq.speckle.systems/ingest/otlp/v1/logs",
        Headers: new() { { "X-Seq-ApiKey", "agZqxG4jQELxQQXh0iZQ" } }
      )
    ),
    new(
      Console: false,
      Otel: new(
        Endpoint: "https://seq.speckle.systems/ingest/otlp/v1/traces",
        Headers: new() { { "X-Seq-ApiKey", "agZqxG4jQELxQQXh0iZQ" } }
      )
    )
  );
  Setup.Initialize(new ServiceCollection(), config);
}
