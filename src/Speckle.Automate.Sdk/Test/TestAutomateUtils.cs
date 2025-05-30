using GraphQL;
using Speckle.Automate.Sdk.Schema;
using Speckle.Automate.Sdk.Schema.Triggers;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models.Responses;

namespace Speckle.Automate.Sdk.Test;

internal class TestAutomationRun
{
  [JsonRequired]
  public required string AutomationRunId { get; init; }

  [JsonRequired]
  public required string FunctionRunId { get; init; }

  [JsonRequired]
  public required IReadOnlyList<TestAutomationRunTrigger> Triggers { get; init; }
}

internal class TestAutomationRunTrigger : AutomationRunTriggerBase
{
  /// <remarks>This should really be a TestAutomationRunTriggerPayload, but right now, they look the samee</remarks>
  public required VersionCreationTriggerPayload Payload { get; init; }
}

public static class TestAutomateUtils
{
  public static async Task<AutomationRunData> CreateTestRun(
    IClient speckleClient,
    CancellationToken cancellationToken = default
  )
  {
    //language=graphql
    const string QUERY = """
      mutation Mutation($projectId: ID!, $automationId: ID!) {
        data:projectMutations {
          data:automationMutations(projectId: $projectId) {
            data:createTestAutomationRun(automationId: $automationId) {
              automationRunId
              functionRunId
              triggers {
                payload {
                  modelId
                  versionId
                }
                triggerType
              }
            }
          }
        }
      }
      """;

    GraphQLRequest request = new(
      query: QUERY,
      variables: new
      {
        automationId = TestAutomateEnvironment.GetSpeckleAutomationId(),
        projectId = TestAutomateEnvironment.GetSpeckleProjectId(),
      }
    );

    var res = await speckleClient
      .ExecuteGraphQLRequest<RequiredResponse<RequiredResponse<RequiredResponse<TestAutomationRun>>>>(
        request,
        cancellationToken
      )
      .ConfigureAwait(false);

    var runData = res.data.data.data;
    var triggerData = runData.Triggers[0].Payload;

    string modelId = triggerData.ModelId;
    string versionId = triggerData.VersionId;

    var data = new AutomationRunData()
    {
      ProjectId = TestAutomateEnvironment.GetSpeckleProjectId(),
      SpeckleServerUrl = TestAutomateEnvironment.GetSpeckleServerUrl(),
      AutomationId = TestAutomateEnvironment.GetSpeckleAutomationId(),
      AutomationRunId = runData.AutomationRunId,
      FunctionRunId = runData.FunctionRunId,
      Triggers = [new(modelId: modelId, versionId: versionId)],
    };

    return data;
  }
}
