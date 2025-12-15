using System.Diagnostics;
using System.Text.Json;
using GraphQL;
using GraphQL.Client.Http;
using Speckle.Automate.Sdk.Schema;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;

namespace Speckle.Automate.Sdk;

[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class AutomationContextFactory(
  IClientFactory clientFactory,
  IAccountFactory accountFactory,
  IOperations operations
) : IAutomationContextFactory
{
  private static readonly JsonSerializerOptions s_jsonSerializerSettings = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

  /// <inheritdoc cref="Initialize(AutomationRunData, string)"/>
  public async Task<IAutomationContext> Initialize(string automationRunData, string speckleToken)
  {
    var runData = JsonSerializer.Deserialize<AutomationRunData>(automationRunData, s_jsonSerializerSettings);

    return await Initialize(runData, speckleToken).ConfigureAwait(false);
  }

  /// <inheritdoc cref="Initialize(AutomationRunData, Account)"/>
  /// <exception cref="GraphQLHttpRequestException">Request failed on the HTTP layer (received a non-successful response code)</exception>
  /// <exception cref="AggregateException"><inheritdoc cref="Speckle.Sdk.Api.GraphQL.GraphQLErrorHandler.EnsureGraphQLSuccess(IGraphQLResponse)"/></exception>
  public async Task<IAutomationContext> Initialize(AutomationRunData automationRunData, string speckleToken)
  {
    Account account = await accountFactory
      .CreateAccount(automationRunData.SpeckleServerUrl, speckleToken)
      .ConfigureAwait(false);

    return Initialize(automationRunData, account);
  }

  /// <summary>
  /// Creates an <see cref="AutomationContext"/> from the provided data
  /// </summary>
  public IAutomationContext Initialize(AutomationRunData automationRunData, Account account)
  {
    IClient client = clientFactory.Create(account);
    Stopwatch initTime = Stopwatch.StartNew();

    return new AutomationContext(operations)
    {
      AutomationRunData = automationRunData,
      SpeckleClient = client,
      _speckleToken = account.token.NotNull("Speckle token was null"),
      _initTime = initTime,
      AutomationResult = new AutomationResult(),
    };
  }
}
