using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Progress;
using Speckle.Sdk.Pipelines.Send;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, 0, 0, 4)]
#pragma warning disable CA1001
public class SendToServerTest
#pragma warning restore CA1001
{
  private SendPipeline _sut;
  private ServiceProvider _provider;
  private Collection _testData;
  private IOperations _operations;
  private IClient _client;
  private ServerTransport _remoteTransport;
  private ISendPipelineFactory _sendPipelineFactory;
  private Project _targetProject;
  private Model _targetModel;
  private ModelIngestion _ingestion;
  private readonly Uri _server_url = new("https://app.speckle.systems");

  [GlobalSetup]
  public async Task Setup()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", assemblies: typeof(Mesh).Assembly);
    _provider = serviceCollection.BuildServiceProvider();

    var account = _provider.GetRequiredService<IAccountManager>().GetAccounts(_server_url).First();
    _client = _provider.GetRequiredService<IClientFactory>().Create(account);
    _operations = _provider.GetRequiredService<IOperations>();
    _sendPipelineFactory = _provider.GetRequiredService<ISendPipelineFactory>();

    _testData = (Collection)
      await _operations.Receive2(_server_url, "bf5b49215c", "feff8c11a06597d3a7740738a55417d2", null, null, default);
  }

  [IterationSetup]
  public void BeforeEach() => BeforeEachAsync().GetAwaiter().GetResult();

  private async Task BeforeEachAsync()
  {
    _targetProject = await _client.Project.CreateInWorkspace(new(null, null, null, "27b0bb90c4"));
    _targetModel = await _client.Model.Create(new("test", null, _targetProject.id));
    _ingestion = await _client.Ingestion.Create(
      new(_targetModel.id, _targetProject.id, "", new("perftest", "1", null, null), 7200)
    );

    _sut?.Dispose();
    _sut = _sendPipelineFactory.CreateInstance(
      _targetProject.id,
      _ingestion.id,
      _client.Account,
      new NullProgress<StreamProgressArgs>(),
      default
    );
    _remoteTransport?.Dispose();
    _remoteTransport = _provider
      .GetRequiredService<IServerTransportFactory>()
      .Create(_client.Account, _targetProject.id);
  }

  [Benchmark]
  public async Task Send3()
  {
    foreach (var item in _testData.elements)
    {
      _ = await _sut.Process(item);
    }
    await _sut.WaitForUpload();
  }

  [Benchmark]
  public async Task<SerializeProcessResults> Send2()
  {
    return await _operations.Send2(_server_url, _targetProject.id, _client.Account.token, _testData, null, default);
  }

  [Benchmark]
  public async Task<(string rootObjId, IReadOnlyDictionary<string, ObjectReference> convertedReferences)> Send1()
  {
    return await _operations.Send(_testData, _remoteTransport, true);
  }
}
