using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive;

public sealed class ReceivePipelineTests
{
  private readonly IOperations _ops;
  private readonly IAccountManager _acc;

  public ReceivePipelineTests()
  {
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _ops = serviceProvider.GetRequiredService<IOperations>();
    _acc = serviceProvider.GetRequiredService<IAccountManager>();
  }

  [Fact]
  public async Task Baz()
  {
    Account acc = _acc.GetAccounts("https://app.speckle.systems").First();
    Base root = await _ops.Receive3("6a678f8f7b", "18dab75e49", "321671961a", acc, null, CancellationToken.None);
    Console.WriteLine(root.id);
  }
}
