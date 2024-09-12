using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance;

public sealed class TestDataHelper : IDisposable
{
  private static readonly string s_basePath = $"./temp {Guid.NewGuid()}";
  public SQLiteTransport Transport { get; private set; }

  public static IServiceProvider ServiceProvider { get; set; }
  public string ObjectId { get; private set; }

  public TestDataHelper()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new SpeckleConfiguration(HostApplications.Navisworks, HostAppVersion.v2023));
    ServiceProvider = serviceCollection.BuildServiceProvider();
  }

  public async Task SeedTransport(Account account, string streamId, string objectId)
  {
    // Transport = new SQLiteTransport(s_basePath, APPLICATION_NAME);
    Transport = new SQLiteTransport();

    //seed SQLite transport with test data
    ObjectId = await SeedTransport(account, streamId, objectId, Transport).ConfigureAwait(false);
  }

  public async Task<string> SeedTransport(Account account, string streamId, string objectId, ITransport transport)
  {
    using ServerTransport remoteTransport = ServiceProvider
      .GetRequiredService<IServerTransportFactory>()
      .Create(account, streamId);
    transport.BeginWrite();
    await remoteTransport.CopyObjectAndChildren(objectId, transport).ConfigureAwait(false);
    transport.EndWrite();
    await transport.WriteComplete().ConfigureAwait(false);

    return objectId;
  }

  public async Task<Base> DeserializeBase()
  {
    return await ServiceProvider
      .GetRequiredService<IOperations>()
      .Receive(ObjectId, null, Transport)
      .ConfigureAwait(false);
  }

  public void Dispose()
  {
    Transport.Dispose();
    SqliteConnection.ClearAllPools();
    Directory.Delete(s_basePath, true);
  }
}
