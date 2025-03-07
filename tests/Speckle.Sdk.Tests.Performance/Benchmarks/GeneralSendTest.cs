using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, iterationCount: 1)]
public class GeneralSendTest
{
  private Base _testData;
  private IOperations _operations;
  private ServerTransport _remote;
  private Account acc;
  private IClient client;

  private Project _project;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    using var dataSource = new TestDataHelper();
    await dataSource
      .SeedTransport(
        new Account() { serverInfo = new() { url = "https://latest.speckle.systems/" } },
        "2099ac4b5f",
        "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6",
        false
      )
      .ConfigureAwait(false);

    SpeckleObjectDeserializer deserializer = new() { ReadTransport = dataSource.Transport };
    string data = await dataSource.Transport.GetObject(dataSource.ObjectId).NotNull();
    _testData = await deserializer.DeserializeAsync(data).NotNull();
    _operations = TestDataHelper.ServiceProvider.GetRequiredService<IOperations>();

    acc = TestDataHelper
      .ServiceProvider.GetRequiredService<IAccountManager>()
      .GetAccounts("https://latest.speckle.systems")
      .First();

    client = TestDataHelper.ServiceProvider.GetRequiredService<IClientFactory>().Create(acc);

    _project = await client.Project.Create(
      new($"General Send Test run {Guid.NewGuid()}", null, ProjectVisibility.Public)
    );
    _remote = TestDataHelper.ServiceProvider.GetRequiredService<IServerTransportFactory>().Create(acc, _project.id);
  }

  [Benchmark(Baseline = true)]
  public async Task<Version> Send_old()
  {
    using SQLiteTransport local = new();
    var res = await _operations.Send(_testData, [_remote, local]);
    return await TagVersion($"Send_old {Guid.NewGuid()}", res.rootObjId);
  }

  [Benchmark]
  public async Task<Version> Send_new()
  {
    var res = await _operations.Send2(new(acc.serverInfo.url), _project.id, acc.token, _testData, null, default);
    return await TagVersion($"Send_new {Guid.NewGuid()}", res.RootId);
  }

  private async Task<Version> TagVersion(string name, string objectId)
  {
    var model = await client.Model.Create(new(name, null, _project.id));
    return await client.Version.Create(new(objectId, model.id, _project.id));
  }
}
