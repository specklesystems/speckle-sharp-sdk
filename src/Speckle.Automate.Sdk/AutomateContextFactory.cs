using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Speckle.Automate.Sdk.Schema;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api;
using Speckle.Sdk.Credentials;

namespace Speckle.Automate.Sdk;

[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class AutomateContextFactory(
  IClientFactory clientFactory,
  IAccountManager accountManager,
  IOperations operations
) : IAutomateContextFactory
{
  public async Task<IAutomationContext> Initialize(string automationRunData, string speckleToken)
  {
    var runData = JsonConvert.DeserializeObject<AutomationRunData>(
      automationRunData,
      new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
    );
    return await Initialize(runData, speckleToken).ConfigureAwait(false);
  }

  public async Task<IAutomationContext> Initialize(AutomationRunData automationRunData, string speckleToken)
  {
    Account account = new()
    {
      token = speckleToken,
      serverInfo = await accountManager.GetServerInfo(automationRunData.SpeckleServerUrl).ConfigureAwait(false),
      userInfo = await accountManager
        .GetUserInfo(speckleToken, automationRunData.SpeckleServerUrl)
        .ConfigureAwait(false),
    };
    return Initialize(automationRunData, account);
  }

  public IAutomationContext Initialize(AutomationRunData automationRunData, Account account)
  {
    IClient client = clientFactory.Create(account);
    Stopwatch initTime = new();
    initTime.Start();

    return new AutomationContext(operations)
    {
      AutomationRunData = automationRunData,
      SpeckleClient = client,
      _speckleToken = account.token,
      _initTime = initTime,
      AutomationResult = new AutomationResult(),
    };
  }
}
