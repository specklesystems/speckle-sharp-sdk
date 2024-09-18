using Microsoft.Data.Sqlite;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Testing;

public sealed class TestDataHelper(IAccountManager accountManager, 
  IClientFactory clientFactory, IServerTransportFactory serverTransportFactory, IOperations operations) : IDisposable
{
  private static readonly string s_basePath = $"./temp {Guid.NewGuid()}";

  public SQLiteTransport Transport { get; private set; }
  public string ObjectId { get; private set; }

  public async Task SeedTransport(StreamWrapper sw)
  {
    // Transport = new SQLiteTransport(s_basePath, APPLICATION_NAME);
    Transport = new SQLiteTransport();

    //seed SQLite transport with test data
    ObjectId = await SeedTransport(sw, Transport).ConfigureAwait(false);
  }

  public async Task<string> SeedTransport( StreamWrapper sw, ITransport transport)
  {
    //seed SQLite transport with test data
    var acc =  sw.GetAccount(accountManager);
    using var client = clientFactory.Create(acc);
    var branch = await client.Model.Get(sw.StreamId, sw.BranchName!, default).ConfigureAwait(false);
    var objectId = branch.id;

    using ServerTransport remoteTransport = serverTransportFactory.Create(acc, sw.StreamId);
    transport.BeginWrite();
    await remoteTransport.CopyObjectAndChildren(objectId, transport).ConfigureAwait(false);
    transport.EndWrite();
    await transport.WriteComplete().ConfigureAwait(false);

    return objectId;
  }

  public async Task<Base> DeserializeBase()
  {
    return await operations.Receive(ObjectId, null, Transport).ConfigureAwait(false);
  }

  public void Dispose()
  {
    Transport.Dispose();
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(s_basePath))
    {
      Directory.Delete(s_basePath, true);
    }
  }
}
