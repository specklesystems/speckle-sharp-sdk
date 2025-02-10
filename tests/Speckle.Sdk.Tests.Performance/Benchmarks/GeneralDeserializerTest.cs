using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.SQLite;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 2)]
public class GeneralDeserializer : IDisposable
{
  private const bool skipCache = true;

  /*
  private const string url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?
  private const string streamId = "a3ac1b2706";
  private const string rootId = "7d53bcf28c6696ecac8781684a0aa006";*/


  private const string url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?
  private const string streamId = "2099ac4b5f";
  private const string rootId = "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6";
  private TestDataHelper _dataSource;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    _dataSource = new TestDataHelper();
    await _dataSource
      .SeedTransport(new Account() { serverInfo = new() { url = url } }, streamId, rootId, skipCache)
      .ConfigureAwait(false);
  }

  [Benchmark]
  public async Task<Base> RunTest_New()
  {
    var sqlite = TestDataHelper
      .ServiceProvider.GetRequiredService<ISqLiteJsonCacheManagerFactory>()
      .CreateFromStream(streamId);
    var serverObjects = new ServerObjectManager(
      TestDataHelper.ServiceProvider.GetRequiredService<ISpeckleHttp>(),
      TestDataHelper.ServiceProvider.GetRequiredService<ISdkActivityFactory>(),
      new Uri(url),
      streamId,
      null
    );
    var o = new ObjectLoader(sqlite, serverObjects, null);
    using var process = new DeserializeProcess(
      null,
      o,
      new BaseDeserializer(new ObjectDeserializerFactory()),
      default,
      new(skipCache)
    );
    return await process.Deserialize(rootId).ConfigureAwait(false);
  }

  /*
    [Benchmark]
    public async Task<Base> RunTest_Old()
    {
      SpeckleObjectDeserializer sut = new() { ReadTransport = _dataSource.Transport };
      string data = await _dataSource.Transport.GetObject(_dataSource.ObjectId)!;
      return await sut.DeserializeAsync(data);
    }
  */
  [GlobalCleanup]
  public void Cleanup() => Dispose();

  public void Dispose() => _dataSource.Dispose();
}
