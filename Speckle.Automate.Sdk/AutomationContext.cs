using System.Diagnostics;
using GraphQL;
using Speckle.Automate.Sdk.Schema;
using Speckle.Automate.Sdk.Schema.Triggers;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Models;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Automate.Sdk;

[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class AutomationContext(IOperations operations) : IAutomationContext
{
  public AutomationRunData AutomationRunData { get; set; }
  public string? ContextView => AutomationResult.ResultView;
  public required IClient SpeckleClient { get; init; }

  public required string _speckleToken { get; init; }

  // added for performance measuring
  public required Stopwatch _initTime { get; init; }

  public required AutomationResult AutomationResult { get; init; }

  public string RunStatus => AutomationResult.RunStatus;

  public string? StatusMessage => AutomationResult.StatusMessage;
  public TimeSpan Elapsed => _initTime.Elapsed;

  /// <summary>
  /// Receive version for automation.
  /// </summary>
  /// <returns> Commit object. </returns>
  /// <exception cref="SpeckleException">Throws if commit object is null.</exception>
  public async Task<Base> ReceiveVersion(CancellationToken cancellationToken = default)
  {
    // TODO: this is a quick hack to keep implementation consistency. Move to proper receive many versions
    if (AutomationRunData.Triggers.First() is not VersionCreationTrigger trigger)
    {
      throw new SpeckleException("Processed automation run data without any triggers");
    }
    var versionId = trigger.Payload.VersionId;

    var version = await SpeckleClient
      .Version.Get(versionId, AutomationRunData.ProjectId, cancellationToken)
      .ConfigureAwait(false);

    if (version.referencedObject == null)
    {
      throw new SpeckleException(
        "The requested speckle model version has exceeded workspace version history limits or the reference object is otherwise null"
      );
    }

    Base? rootObject = await operations
      .Receive2(
        SpeckleClient.ServerUrl,
        AutomationRunData.ProjectId,
        version.referencedObject,
        SpeckleClient.Account.token,
        null,
        cancellationToken
      )
      .ConfigureAwait(false);

    Console.WriteLine($"It took {Elapsed.TotalSeconds} seconds to receive the speckle version {versionId}");
    return rootObject;
  }

  /// <summary>
  /// Creates new version in the project.
  /// </summary>
  /// <param name="rootObject">Object to send to project.</param>
  /// <param name="model">The model to create the version under</param>
  /// <param name="versionMessage">Version message.</param>
  /// <param name="cancellationToken">Version message.</param>
  /// <returns>Version id.</returns>
  /// <exception cref="SpeckleException"> Throws if given model name is as same as with model name in automation run data.
  /// The reason is to prevent circular run loop in automation.</exception>
  public async Task<Version> CreateNewVersionInProject(
    Base rootObject,
    Model model,
    string versionMessage = "",
    CancellationToken cancellationToken = default
  )
  {
    // Confirm target branch is not the same as source branch
    foreach (var trigger in AutomationRunData.Triggers)
    {
      if (trigger == null)
      {
        continue;
      }

      if (trigger.Payload.ModelId == model.id)
      {
        throw new SpeckleException(
          $"""
          The target model: {model.name} ({model.id}) cannot match the model
           that triggered this automation:
           {trigger.Payload.ModelId}
          """
        );
      }
    }

    var (rootObjectId, _) = await operations
      .Send2(
        SpeckleClient.ServerUrl,
        AutomationRunData.ProjectId,
        SpeckleClient.Account.token,
        rootObject,
        null,
        cancellationToken
      )
      .ConfigureAwait(false);

    var newVersion = await SpeckleClient
      .Version.Create(
        new CreateVersionInput(rootObjectId, model.id, AutomationRunData.ProjectId, versionMessage),
        cancellationToken
      )
      .ConfigureAwait(false);

    AutomationResult.ResultVersions.Add(newVersion.id);

    return newVersion;
  }

  /// <summary>
  /// Set context view for automation result view.
  /// </summary>
  /// <param name="resourceIds"> Resource contexts to bind into view.</param>
  /// <param name="includeSourceModelVersion"> Whether bind source version into result view or not.</param>
  /// <exception cref="SpeckleException"> Throws if there is no context to create result view.</exception>
  public void SetContextView(List<string>? resourceIds = null, bool includeSourceModelVersion = true)
  {
    List<string> linkResources = new();
    if (includeSourceModelVersion)
    {
      foreach (var trigger in AutomationRunData.Triggers)
      {
        switch (trigger)
        {
          case VersionCreationTrigger versionCreationTrigger:
          {
            linkResources.Add($@"{versionCreationTrigger.Payload.ModelId}@{versionCreationTrigger.Payload.VersionId}");
            break;
          }
          default:
          {
            throw new SpeckleException($"Could not link resource specified by {trigger.TriggerType} trigger");
          }
        }
      }
    }

    if (resourceIds is not null)
    {
      linkResources.AddRange(resourceIds);
    }

    if (linkResources.Count == 0)
    {
      throw new SpeckleException("We do not have enough resource ids to compose a context view");
    }

    AutomationResult.ResultView = $"/projects/{AutomationRunData.ProjectId}/models/{string.Join(",", linkResources)}";
  }

  public async Task ReportRunStatus()
  {
    ObjectResults? objectResults = null;
    if (RunStatus is "SUCCEEDED" or "FAILED")
    {
      objectResults = new ObjectResults
      {
        Values = new ObjectResultValues
        {
          BlobIds = AutomationResult.Blobs,
          ObjectResults = AutomationResult.ObjectResults,
        },
      };
    }
    GraphQLRequest request = new()
    {
      Query =
        @"
            mutation AutomateFunctionRunStatusReport(
                $projectId: String!
                $functionRunId: String!
                $status: AutomateRunStatus!
                $statusMessage: String
                $results: JSONObject
                $contextView: String
            ){
                automateFunctionRunStatusReport(input: {
                    projectId: $projectId
                    functionRunId: $functionRunId
                    status: $status
                    statusMessage: $statusMessage
                    contextView: $contextView
                    results: $results
                })
            }
        ",
      Variables = new
      {
        projectId = AutomationRunData.ProjectId,
        functionRunId = AutomationRunData.FunctionRunId,
        status = RunStatus,
        statusMessage = AutomationResult.StatusMessage,
        contextView = ContextView,
        results = objectResults,
      },
    };
    await SpeckleClient.ExecuteGraphQLRequest<Dictionary<string, object>>(request).ConfigureAwait(false);
  }

  /// <summary>
  /// Stores result file in automation result. It will be available to download on Frontend if added.
  /// </summary>
  /// <param name="filePath"> File path to store.</param>
  /// <exception cref="FileNotFoundException"> Throws if given file path is not exist.</exception>
  /// <exception cref="SpeckleException"> Throws if upload requests return no result.</exception>
  public async Task StoreFileResult(string filePath)
  {
    if (!File.Exists(filePath))
    {
      throw new FileNotFoundException("The given file path doesn't exist", fileName: filePath);
    }

    using MultipartFormDataContent formData = new();
    FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
    using StreamContent streamContent = new(fileStream);
    formData.Add(streamContent, "files", Path.GetFileName(filePath));
    HttpResponseMessage? request = await SpeckleClient
      .GQLClient.HttpClient.PostAsync(
        new Uri($"{AutomationRunData.SpeckleServerUrl}api/stream/{AutomationRunData.ProjectId}/blob"),
        formData
      )
      .ConfigureAwait(false);
    request.EnsureSuccessStatusCode();
    string? responseString = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
    Console.WriteLine("RESPONSE - " + responseString);
    BlobUploadResponse uploadResponse = JsonConvert.DeserializeObject<BlobUploadResponse>(responseString);
    if (uploadResponse.UploadResults.Count != 1)
    {
      throw new SpeckleException("Expected one upload result.");
    }

    AutomationResult.Blobs.AddRange(uploadResponse.UploadResults.Select(r => r.BlobId));
  }

  private void MarkRun(AutomationStatus status, string? statusMessage)
  {
    double duration = Elapsed.TotalSeconds;
    AutomationResult.StatusMessage = statusMessage;
    string statusValue = AutomationStatusMapping.Get(status);
    AutomationResult.RunStatus = statusValue;
    AutomationResult.Elapsed = duration;

    string msg = $"Automation run {statusValue} after {duration} seconds.";
    if (statusMessage is not null)
    {
      msg += $"\n{statusMessage}";
    }

    Console.WriteLine(msg);
  }

  public void MarkRunFailed(string statusMessage) => MarkRun(AutomationStatus.Failed, statusMessage);

  public void MarkRunException(string? statusMessage) => MarkRun(AutomationStatus.Exception, statusMessage);

  public void MarkRunSuccess(string? statusMessage) => MarkRun(AutomationStatus.Succeeded, statusMessage);

  /// <summary>
  /// Add a new error case to the run results.
  /// If the error cause has already created an error case,
  /// the case will be extended with a new case referring to the causing objects.
  /// </summary>
  /// <param name="category">A short tag for the error type.</param>
  /// <param name="objectIds">A list of objectId's that are causing the error.</param>
  /// <param name="message">Optional error message.</param>
  /// <param name="metadata">User provided metadata key value pairs.</param>
  /// <param name="visualOverrides">Case specific 3D visual overrides.</param>
  /// <exception cref="ArgumentException">Throws if the provided <paramref name="objectIds"/> input is empty.</exception>
  public void AttachErrorToObjects(
    string category,
    IEnumerable<string> objectIds,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Error, category, objectIds, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new warning case to the run results.
  /// If the warning cause has already created a warning case,
  /// the case will be extended with a new case referring to the causing objects.
  /// </summary>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachWarningToObjects(
    string category,
    IEnumerable<string> objectIds,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Warning, category, objectIds, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new info case to the run results.
  /// If the info cause has already created an info case,
  /// the case will be extended with a new case referring to the causing objects.
  /// </summary>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachInfoToObjects(
    string category,
    IEnumerable<string> objectIds,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Info, category, objectIds, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new success case to the run results.
  /// If the success cause has already created a success case,
  /// the case will be extended with a new case referring to the causing objects.
  /// </summary>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachSuccessToObjects(
    string category,
    IEnumerable<string> objectIds,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Success, category, objectIds, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new case to the run results.
  /// If the cause has already created an case with equal level,
  /// the case will be extended with a new case referring to the causing objects.
  /// </summary>
  /// <param name="level">The level assigned to this result.</param>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachResultToObjects(
    ObjectResultLevel level,
    string category,
    IEnumerable<string> objectIds,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  )
  {
    string levelString = ObjectResultLevelMapping.Get(level);
    List<string> objectIdList = objectIds.ToList();
    if (objectIdList.Count == 0)
    {
      throw new ArgumentException($"Need at least one object_id to report a(n) {level}");
    }

    Console.WriteLine($"Created new {levelString.ToUpper()} category: {category} caused by: {message}");

    ResultCase resultCase = new()
    {
      Category = category,
      Level = levelString,
      ObjectIds = objectIdList,
      Message = message,
      Metadata = metadata,
      VisualOverrides = visualOverrides,
    };

    AutomationResult.ObjectResults.Add(resultCase);
  }
}
