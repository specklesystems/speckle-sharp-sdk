using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Progress;
using Speckle.Sdk.Pipelines.Receive;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// <code>
///  | DegreeOfParal | Mean     | Error   | StdDev   | Allocated |
///  |-------------- |---------:|--------:|---------:|----------:|
///  | 1             | 28.555 s | 1.541 s | 0.2384 s |   5.25 GB |
///  | 2             | 30.406 s | 2.877 s | 0.4452 s |   8.98 GB |
///  | 3             |  9.514 s | 2.784 s | 0.4309 s |   4.68 GB |
///  | 4             |  6.568 s | 2.426 s | 0.3754 s |   4.33 GB |
///  | 5             |  5.622 s | 1.753 s | 0.2713 s |   4.24 GB |
///  | 6             |  5.077 s | 1.700 s | 0.2630 s |   4.12 GB |
///  | 8             |  4.910 s | 3.295 s | 0.5100 s |   4.18 GB |
///  | 12            |  4.869 s | 3.997 s | 0.6186 s |   4.12 GB |
///  | 16            |  4.807 s | 1.532 s | 0.2371 s |   4.12 GB |
///  | 32            |  4.504 s | 1.991 s | 0.3081 s |   4.04 GB |
///  | 48            |  4.625 s | 1.236 s | 0.1912 s |   4.03 GB |
///  | 64            |  4.808 s | 2.497 s | 0.3864 s |   4.06 GB |
/// </code>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 4)]
public class PipelineDeserialize : IDisposable
{
  private ServiceProvider _provider;
  private ReceivePipeline _sut;
  private DisposableFile _duckFile;
  private ISdkActivityFactory _activityFactory;

  private IClient _client;

  [GlobalSetup]
  public async Task Setup()
  {
    {
      var serviceCollection = new ServiceCollection();
      serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", assemblies: typeof(Mesh).Assembly);
      _provider = serviceCollection.BuildServiceProvider();
    }

    _activityFactory = _provider.GetRequiredService<ISdkActivityFactory>();

    {
      using var acc = _provider.GetRequiredService<IAccountManager>();
      var account = acc.GetAccounts(new Uri("https://app.speckle.systems")).First();
      _client = _provider.GetRequiredService<IClientFactory>().Create(account);
    }

    {
      var project = await _client.Project.Get("321671961a");
      var model = await _client.Model.Get("81e90975ec", project.id);
      var version = await _client.Version.Get("78e73217e6", project.id);

      _sut = _provider
        .GetRequiredService<IReceivePipelineFactory>()
        .CreateInstance(version, model, project, _client.Account);
    }

    {
      var logger = _provider.GetRequiredService<ILogger<PipelineDeserialize>>();
      _duckFile = new DisposableFile(new FileInfo(Path.GetTempFileName()), logger);
      await _sut.DownloadDuckFile(_duckFile.FileInfo, new NullProgress<StreamProgressArgs>(), CancellationToken.None);
    }
  }

  [Params(1, 2, 3, 4, 6, 8, 16, 32)]
  public int MaxDegreeOfParallelism { get; set; }

  [Benchmark]
  public async Task<Base> Receive3()
  {
    using PackFileManager packFileManager = new(_duckFile.FileInfo, _activityFactory);
    var deserializer = new SpeckleObjectDeserializer(packFileManager, MaxDegreeOfParallelism, CancellationToken.None);

    Base result = await deserializer.MaterializeGraphAsync(new NullProgress<CardProgress>());
    return result;
  }

  [GlobalCleanup]
  public void Dispose()
  {
    _duckFile?.Dispose();
    _client?.Dispose();
    _sut.Dispose();
    _activityFactory?.Dispose();

    _provider?.Dispose();
  }
}
