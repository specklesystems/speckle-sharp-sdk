using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[RankColumn]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 4)]
public class GeneralReceive
{
  private const string URL =
    "https://latest.speckle.systems/projects/a3ac1b2706/models/7d53bcf28c6696ecac8781684a0aa006"; //small?

  // "https://latest.speckle.systems/projects/2099ac4b5f/models/30fb4cbe6eb2202b9e7b4a4fcc3dd2b6"; //perf?

  [GlobalSetup]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    /* _dataSource = new TestDataHelper();
     await _dataSource
       .SeedTransport(new("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"))
       .ConfigureAwait(false);*/
  }

  [Benchmark]
  public Task<Base> TwoDownloadFourDeserializer()
  {
    return Receive2Test(
      new ReceiveProcessSettings()
      {
        MaxDownloadThreads = 2,
        MaxDeserializeThreads = 4,
        SqliteManagerOptions = new(false)
      }
    );
  }

  [Benchmark]
  public Task<Base> OneDownloadOneDeserializer()
  {
    return Receive2Test(
      new ReceiveProcessSettings()
      {
        MaxDownloadThreads = 1,
        MaxDeserializeThreads = 1,
        SqliteManagerOptions = new(false)
      }
    );
  }

  [Benchmark]
  public Task<Base> FourDownloadFourDeserializer()
  {
    return Receive2Test(
      new ReceiveProcessSettings()
      {
        MaxDownloadThreads = 4,
        MaxDeserializeThreads = 4,
        SqliteManagerOptions = new(false)
      }
    );
  }

  [Benchmark]
  public Task<Base> FourDownload8Deserializer()
  {
    return Receive2Test(
      new ReceiveProcessSettings()
      {
        MaxDownloadThreads = 4,
        MaxDeserializeThreads = 8,
        SqliteManagerOptions = new(false)
      }
    );
  }

  [Benchmark]
  public Task<Base> TwoDownloadFourDeserializerHalfMaxSize()
  {
    return Receive2Test(
      new ReceiveProcessSettings()
      {
        MaxDownloadThreads = 2,
        MaxDeserializeThreads = 4,
        MaxObjectRequestSize = 5000,
        SqliteManagerOptions = new(false)
      }
    );
  }

  private async Task<Base> Receive2Test(ReceiveProcessSettings settings)
  {
    /*SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeJsonAsync(data);*/

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
    var serviceProvider = serviceCollection.BuildServiceProvider();
    var operations = serviceProvider.GetRequiredService<IOperations>();

    StreamWrapper sw = new(URL);
    var acc = serviceProvider.GetRequiredService<IAccountManager>().GetDefaultAccount().NotNull();
    var rootObject = await operations.Receive2(sw.ObjectId.NotNull(), sw.ProjectId, acc, settings);

    return rootObject;
  }

  [Benchmark]
  public async Task<Base> Receive1Test()
  {
    /*SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeJsonAsync(data);*/

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
    var serviceProvider = serviceCollection.BuildServiceProvider();
    var operations = serviceProvider.GetRequiredService<IOperations>();

    StreamWrapper sw = new(URL);
    var acc = serviceProvider.GetRequiredService<IAccountManager>().GetDefaultAccount().NotNull();

    using var serverTransport = serviceProvider.GetRequiredService<IServerTransportFactory>().Create(acc, sw.ProjectId);
    MemoryTransport local = new();
    var rootObject = await operations.Receive(sw.ObjectId.NotNull(), serverTransport, local);

    return rootObject;
  }
}
