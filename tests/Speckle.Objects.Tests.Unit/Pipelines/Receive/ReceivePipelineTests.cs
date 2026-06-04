using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Tests.Unit.Pipelines.Receive;

public sealed class ReceivePipelineTests
{
  private readonly IOperations _ops;
  private readonly IAccountManager _acc;

  public ReceivePipelineTests()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(Mesh).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    _ops = serviceProvider.GetRequiredService<IOperations>();
    _acc = serviceProvider.GetRequiredService<IAccountManager>();
  }

  [Fact]
  public async Task Baz3()
  {
    Account acc = _acc.GetAccounts("https://app.speckle.systems").First();
    // Graphisoft
    Base root = await _ops.Receive3("78e73217e6", "81e90975ec", "321671961a", acc, null, CancellationToken.None);

    // Small
    // Base root = await _ops.Receive3("6a678f8f7b", "18dab75e49", "321671961a", acc, null, CancellationToken.None);
    Console.WriteLine(root.id);
  }

  [Fact]
  public async Task Baz2()
  {
    Account acc = _acc.GetAccounts("https://app.speckle.systems").First();
    // Graphisoft
    Base root = await _ops.Receive2(
      new(acc.serverInfo.url),
      "321671961a",
      "b04a2ac7435efdaec6e9b3a142ff203d",
      acc.token,
      null,
      CancellationToken.None
    );

    Console.WriteLine(root.id);
  }

  [Fact]
  public async Task Baz22()
  {
    // Graphisoft
    //language=json
    string json = """
      {
      "id": "8a12398636314f5cbb30eb33c4e240ca",
      "value": {
        "id": "6a4ba8ad613eba7640f3d222dd722a76",
        "name": "DefaultMaterial",
        "diffuse": 4289901234,
        "opacity": 1,
        "emissive": 0,
        "metalness": 0,
        "roughness": 1,
        "speckle_type": "Objects.Other.RenderMaterial",
        "applicationId": "782334533377160842",
        "totalChildrenCount": 0
        }
      }
      """;
    Base root = await _ops.DeserializeAsync(json);

    Console.WriteLine(root.id);
  }
}
