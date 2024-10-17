using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Receive;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 4)]
public class GeneralDeserializer : IDisposable
{
  private TestDataHelper _dataSource;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    _dataSource = new TestDataHelper();
    await _dataSource
      .SeedTransport(
        new Account()
        {
          serverInfo = new() { url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e" },
        },
        "2099ac4b5f",
        "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6"
      )
      .ConfigureAwait(false);
  }

  [Benchmark]
  public async Task<Base> RunTest_New()
  {
    var sqliteTransport = _dataSource.Transport;
    using var o = new ObjectLoader(
      TestDataHelper.ServiceProvider.GetRequiredService<ISpeckleHttp>(),
      TestDataHelper.ServiceProvider.GetRequiredService<ISdkActivityFactory>(),
      new Uri("https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"),
      "2099ac4b5f",
      null,
      null,
      sqliteTransport
    );
    using var process = new DeserializeProcess(null, o);
    return await process.Deserialize(_dataSource.ObjectId).ConfigureAwait(false);
  }

  [Benchmark]
  public async Task<Base> RunTest_Old()
  {
    SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
    string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
    return await sut.DeserializeAsync(data);
  }

  [GlobalCleanup]
  public void Cleanup() => Dispose();

  public void Dispose() => _dataSource.Dispose();
}
