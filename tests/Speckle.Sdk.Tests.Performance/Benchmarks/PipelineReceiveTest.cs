using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// Compare receive1 to receive2 to receive3 (e2e cache miss)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 6)]
public class PipelineReceiveTests : IDisposable
{
  private ServiceProvider _provider;
  private IOperations _sut;
  private IServerTransportFactory _serverTransportFactory;
  private Project _project;
  private Model _model;
  private Version _version;

  private IClient _client;
  private Account Account => _client.Account;

  [GlobalSetup]
  public async Task Setup()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", assemblies: typeof(Mesh).Assembly);
    _provider = serviceCollection.BuildServiceProvider();
    _sut = _provider.GetRequiredService<IOperations>();
    _serverTransportFactory = _provider.GetRequiredService<IServerTransportFactory>();
    using var acc = _provider.GetRequiredService<IAccountManager>();

    var account = acc.GetAccounts(new Uri("https://app.speckle.systems")).First();
    _client = _provider.GetRequiredService<IClientFactory>().Create(account);
    _project = await _client.Project.Get("321671961a");
    _model = await _client.Model.Get("81e90975ec", _project.id);
    _version = await _client.Version.Get("78e73217e6", _project.id);
  }

  [Benchmark]
  public async Task<Base> Receive1()
  {
    using ServerTransport remote = _serverTransportFactory.Create(Account, _project.id);
    using SQLiteTransport local = new();
    return await _sut.Receive(_version.referencedObject!, remote, local);
  }

  [Benchmark]
  public async Task<Base> Receive13()
  {
    return await _sut.Receive13(_version.id!, _model.id, _project.id, Account, null, CancellationToken.None);
  }

  [Benchmark]
  public async Task<Base> Receive2()
  {
    return await _sut.Receive2(
      _client.ServerUrl,
      _project.id,
      _version.referencedObject!,
      Account.token,
      null,
      CancellationToken.None
    );
  }

  [Benchmark]
  public async Task<Base> Receive3()
  {
    return await _sut.Receive3(_version, _model, _project, Account, null, CancellationToken.None);
  }

  [IterationCleanup]
  public void Cleanup()
  {
    try
    {
      File.Delete($"{SpecklePathProvider.UserApplicationDataPath()}/Speckle/Data.db");
    }
    catch (IOException) { }
    File.Delete($"{SpecklePathProvider.UserApplicationDataPath()}/Speckle/Projects/{_project.id}.db");
  }

  [GlobalCleanup]
  public void Dispose()
  {
    _client?.Dispose();
    _provider?.Dispose();
  }
}
