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

// [MemoryDiagnoser]
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

  // [Benchmark(Baseline = true)]
  // public Base Receive3_serial()
  // {
  //   using PackFileManager packFileManager = new(_duckFile.FileInfo, _activityFactory);
  //   var deserializer = new SpeckleObjectDeserializer(packFileManager);
  //
  //   Base result = deserializer.GetCompleteObjectsTreeSerial();
  //   return result;
  // }

  // [Params(1, 2, 3, 4, 6, 8, 16, 32)]
  // public int MaxDegreeOfParallelism { get; set; }

  [Benchmark]
  public Base Receive3_sync()
  {
    using PackFileManager packFileManager = new(_duckFile.FileInfo, _activityFactory);
    var deserializer = new SpeckleObjectDeserializer(packFileManager);

    Base result = deserializer.GetCompleteObjectsTreeSync(CancellationToken.None);
    return result;
  }

  // [Benchmark]
  // public async Task<Base> Receive3_async()
  // {
  //   using PackFileManager packFileManager = new(_duckFile.FileInfo, _activityFactory);
  //   var deserializer = new SpeckleObjectDeserializer(packFileManager);
  //
  //   Base result = await deserializer.GetCompleteObjectsTreeAsync(CancellationToken.None);
  //   return result;
  // }

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
