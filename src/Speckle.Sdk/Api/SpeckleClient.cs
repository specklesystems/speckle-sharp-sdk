using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Api;

public static class SpeckleExtensions
{
  public static ISpeckle GetSpeckle(this IServiceProvider serviceProvider) =>
    serviceProvider.GetRequiredService<ISpeckle>();
}

public interface ISpeckle
{
  Task<ISpeckleClient> Create(Uri url, string? token);
  Task<ISpeckleClient> Create(Account account);
}

public interface ISpeckleClient
{
  IClient Client { get; }

  Task Send(string projectId, Base root, CancellationToken ct = default, IProgress<ProgressArgs>? progress = null);

  Task<Base> Receive(
    string projectId,
    string commitId,
    CancellationToken ct = default,
    IProgress<ProgressArgs>? progress = null
  );
}

internal class SpeckleHelper(AccountManager accountManager, IClientFactory clientFactory, IOperations operations)
  : ISpeckle,
    ISpeckleClient
{
  public async Task<ISpeckleClient> Create(Uri url, string? token)
  {
    var response = await accountManager.GetUserServerInfo(token, url);
    var account = new Account
    {
      token = token!,
      serverInfo = response.serverInfo,
      userInfo = response.activeUser,
    };
    Client = clientFactory.Create(account);
    return this;
  }

  public Task<ISpeckleClient> Create(Account account)
  {
    Client = clientFactory.Create(account);
    return Task.FromResult<ISpeckleClient>(this);
  }

  public IClient Client { get; private set; }

  public async Task Send(
    string projectId,
    Base root,
    CancellationToken ct = default,
    IProgress<ProgressArgs>? progress = null
  ) => await operations.Send2(Client.NotNull().ServerUrl, projectId, Client.Account.token, root, progress, ct);

  public async Task<Base> Receive(
    string projectId,
    string commitId,
    CancellationToken ct = default,
    IProgress<ProgressArgs>? progress = null
  ) => await operations.Receive2(Client.NotNull().ServerUrl, projectId, commitId, Client.Account.token, progress, ct);
}
