using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Models;

namespace Speckle.Sdk.Tests.Integration;

public sealed class SendReceiveTests : IAsyncLifetime
{
  private Project _project;
  private IClient _client;
  private IOperations _operations;
  private const string NON_EXISTENT_OBJECT_ID = "0a480dfb7aa774f19a82bee9d6320abd";
  private const string NON_EXISTENT_PROJECT_ID = "8cdc651d13";

  public async Task InitializeAsync()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
    ClearCache();

    _client = await Fixtures.SeedUserWithClient();
    _project = await _client.Project.Create(new("Blobber", "Flobber", ProjectVisibility.Private));
  }

  [Fact]
  public async Task SendAndReceive()
  {
    var myObject = Fixtures.GenerateNestedObject();
    string expectedId = myObject.GetId(true);

    //SEND
    var fistSend = await _operations.Send2(
      _client.ServerUrl,
      _project.id,
      _client.Account.token,
      myObject,
      null,
      CancellationToken.None
    );

    Assert.Equal(expectedId, fistSend.RootId);
    await Verify(fistSend);

    //RECEIVE
    var received = await _operations.Receive2(
      _client.ServerUrl,
      _project.id,
      fistSend.RootId,
      _client.Account.token,
      null,
      CancellationToken.None
    );

    Assert.Equal(expectedId, received.id);

    //SEND AGAIN!
    var secondSend = await _operations.Send2(
      _client.ServerUrl,
      _project.id,
      _client.Account.token,
      received,
      null,
      CancellationToken.None
    );

    Assert.Equal(expectedId, secondSend.RootId);

    //RECEIVE AGAIN, but using cache
    ClearCache();
    var secondReceive = await _operations.Receive2(
      _client.ServerUrl,
      _project.id,
      fistSend.RootId,
      _client.Account.token,
      null,
      CancellationToken.None
    );

    Assert.Equal(expectedId, secondReceive.id);
  }

  private void ClearCache() { }

  [Fact]
  public async Task ReceiveNonExistentObjectThrows()
  {
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
    {
      _ = await _operations.Receive2(
        _client.ServerUrl,
        _project.id,
        NON_EXISTENT_OBJECT_ID,
        _client.Account.token,
        null,
        CancellationToken.None,
        new(true)
      );
    });
    await Verify(ex);
  }

  [Fact]
  public async Task ReceiveNonExistentProjectThrows()
  {
    var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
    {
      _ = await _operations.Receive2(
        _client.ServerUrl,
        NON_EXISTENT_PROJECT_ID,
        NON_EXISTENT_OBJECT_ID,
        _client.Account.token,
        null,
        CancellationToken.None,
        new(true)
      );
    });
    await Verify(ex);
  }

  [Fact]
  public async Task SendInvalidData()
  {
    var myObject = Fixtures.GenerateNestedObject();
    myObject["invalidProp"] = new StringBuilder(); //Serializer does not support serializing this type

    var ex = await Assert.ThrowsAsync<SpeckleException>(async () =>
    {
      _ = await _operations.Send2(
        _client.ServerUrl,
        _project.id,
        _client.Account.token,
        myObject,
        null,
        CancellationToken.None,
        new(SkipCacheRead: true, SkipCacheWrite: true)
      );
    });
    await Verify(ex);
  }

  [Fact]
  public async Task ReceiveNonAuthThrows()
  {
    using IClient unauthed = Fixtures.Unauthed;
    await Assert.ThrowsAsync<HttpRequestException>(async () =>
    {
      _ = await _operations.Receive2(
        unauthed.ServerUrl,
        _project.id,
        NON_EXISTENT_OBJECT_ID,
        unauthed.Account.token,
        null,
        CancellationToken.None,
        new(true)
      );
    });
  }

  [Fact]
  public async Task ReceiveCancellation()
  {
    using CancellationTokenSource ct = new();
    await ct.CancelAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
    {
      _ = await _operations.Receive2(
        _client.ServerUrl,
        _project.id,
        NON_EXISTENT_OBJECT_ID,
        _client.Account.token,
        null,
        ct.Token,
        new(true)
      );
    });
  }

  public Task DisposeAsync()
  {
    _client?.Dispose();
    return Task.CompletedTask;
  }
}
