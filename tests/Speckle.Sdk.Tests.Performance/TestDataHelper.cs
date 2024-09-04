using Microsoft.Data.Sqlite;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance;

public sealed class TestDataHelper : IDisposable
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

  public static async Task<string> SeedTransport(StreamWrapper sw, ITransport transport)
  {
    //seed SQLite transport with test data
    var acc = await sw.GetAccount().ConfigureAwait(false);
    using var client = new Client(acc);
    var branch = await client.BranchGet(sw.StreamId, sw.BranchName!, 1).ConfigureAwait(false);
    var objectId = branch.commits.items[0].referencedObject;

    using ServerTransport remoteTransport = new(acc, sw.StreamId);
    transport.BeginWrite();
    await remoteTransport.CopyObjectAndChildren(objectId, transport).ConfigureAwait(false);
    transport.EndWrite();
    await transport.WriteComplete().ConfigureAwait(false);

    return objectId;
  }

  public async Task<Base> DeserializeBase()
  {
    return await Operations.Receive(ObjectId, null, Transport).ConfigureAwait(false);
  }

  public void Dispose()
  {
    Transport.Dispose();
    SqliteConnection.ClearAllPools();
    Directory.Delete(s_basePath, true);
  }
}
