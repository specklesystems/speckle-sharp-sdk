using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// How many threads on our Deserializer is optimal
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 1)]
public class GeneralReceiveTest : IDisposable
{
  /*
  private const string url = "https://latest.speckle.systems/projects/a3ac1b2706/models/59d3b0f3c6"; //small?
  private const string streamId = "a3ac1b2706";S
  private const string rootId = "7d53bcf28c6696ecac8781684a0aa006";*/


  private const string url = "https://latest.speckle.systems/projects/2099ac4b5f/models/da511c4d1e"; //perf?
  private readonly Uri _baseUrl = new("https://latest.speckle.systems");
  private const string streamId = "2099ac4b5f";
  private const string rootId = "30fb4cbe6eb2202b9e7b4a4fcc3dd2b6";
  private TestDataHelper _dataSource;
  private IOperations _operations;
  private ITransport remoteTransport;

  [GlobalSetup]
  public async Task Setup()
  {
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Point).Assembly);
    _dataSource = new TestDataHelper();
    var acc = new Account() { serverInfo = new() { url = url } };
    await _dataSource.SeedTransport(acc, streamId, rootId, true).ConfigureAwait(false);
    _operations = TestDataHelper.ServiceProvider.GetRequiredService<IOperations>();
    // await _operations.Receive2(_baseUrl, streamId, rootId, null);

    remoteTransport = TestDataHelper
      .ServiceProvider.GetRequiredService<IServerTransportFactory>()
      .Create(acc, streamId);
  }

  [Benchmark]
  public async Task<Base> RunTest_Receive()
  {
    return await _operations.Receive(rootId, remoteTransport, _dataSource.Transport);
  }

  [Benchmark]
  public async Task<Base> RunTest_Receive2()
  {
    return await _operations.Receive2(_baseUrl, streamId, rootId, null);
  }

  [GlobalCleanup]
  public void Cleanup() => Dispose();

  public void Dispose() => _dataSource.Dispose();
}
