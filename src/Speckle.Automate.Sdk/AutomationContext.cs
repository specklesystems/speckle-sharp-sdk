using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GraphQL;
using Microsoft.Extensions.Logging;
using Speckle.Automate.Sdk.Schema;
using Speckle.Automate.Sdk.Schema.Triggers;
using Speckle.InterfaceGenerator;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Inputs;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Common;
using Speckle.Sdk.Models;
using Model = Speckle.Sdk.Api.GraphQL.Models.Model;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Automate.Sdk;

[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class AutomationContext(IOperations operations, ILogger<AutomationContext> logger) : IAutomationContext
{
  public AutomationRunData AutomationRunData { get; set; }
  public string? ContextView
  {
    get => AutomationResult.ResultView;
    private set => AutomationResult.ResultView = value;
  }
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

    // Automate's function-author surface still hands out a Base tree; its bundle-era successor is a separate effort
    // (atlas spec 2026-08-big-truck-dev-compat, Out of Scope §Automate). Until then it rides the compat projection.
#pragma warning disable CS0618 // Receive2 is obsolete
    Base rootObject = await operations
      .Receive2(
        SpeckleClient.ServerUrl,
        AutomationRunData.ProjectId,
        version.referencedObject,
        SpeckleClient.Account.token,
        null,
        cancellationToken
      )
      .ConfigureAwait(false);
#pragma warning restore CS0618
    // Receive2 marks a bundle-backed version as received itself; a server on Speckle 2026.9.0 serves every version as a
    // bundle, so the explicit markReceived that used to live here would only double-count.
    logger.LogInformation(
      "It took {TotalSeconds} seconds to receive the speckle version {VersionId}",
      Elapsed.TotalSeconds,
      versionId
    );
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
    GuardAgainstCircularTrigger(model);

#pragma warning disable CS0618 // legacy publish pair stays until the ingestion-backed helper lands (ENG-9420)
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

#pragma warning restore CS0618

    AutomationResult.ResultVersions.Add(newVersion.id);

    return newVersion;
  }

  /// <summary>
  /// Publishes a producer-built bundle as a new version over the ingestion rail (Speckle 2026.9.0 send path).
  /// Returns as soon as the upload completes: the <see cref="SendResult.VersionId"/> is allocated and recorded in
  /// <see cref="AutomationResult.ResultVersions"/> immediately, but the version itself is born when the server
  /// finishes ingesting.
  /// <para><b>Finish the wait before you finish the run.</b> If your function returns while the ingestion is still
  /// processing, the run's result links point at a version that does not exist yet (a viewer link 404s until the
  /// server completes it — usually seconds). Unless your function is fire-and-forget, end with:</para>
  /// <code>
  /// var sent = await context.CreateNewVersionInProject(bundle, model, "Analysis results");
  /// await context.WaitForVersion(sent);   // version exists before the run reports success
  /// </code>
  /// <para>Requires a server with the v2 data endpoints (the call throws a clear <see cref="SpeckleException"/>
  /// otherwise).</para>
  /// </summary>
  /// <exception cref="SpeckleException">The target model matches a triggering model (circular run), or the server
  /// did not pre-allocate a version id.</exception>
  public async Task<SendResult> CreateNewVersionInProject(
    BundleBuilder bundle,
    Model model,
    string versionMessage = "",
    CancellationToken cancellationToken = default
  )
  {
    GuardAgainstCircularTrigger(model);

    var sent = await operations
      .Send3(
        SpeckleClient.Account,
        AutomationRunData.ProjectId,
        model.id,
        bundle,
        new SendOptions(Message: versionMessage),
        cancellationToken
      )
      .ConfigureAwait(false);

    AutomationResult.ResultVersions.Add(sent.VersionId);

    return sent;
  }

  /// <summary>Waits for the server to finish ingesting a publish and returns the materialized version. Call this
  /// before your function returns (see <see cref="CreateNewVersionInProject(BundleBuilder, Model, string, CancellationToken)"/>)
  /// so the run's result versions exist by the time the run reports success.</summary>
  /// <exception cref="SpeckleException">The ingestion ended in a non-success terminal status.</exception>
  /// <exception cref="TimeoutException"><paramref name="timeout"/> elapsed first.</exception>
  public Task<Version> WaitForVersion(
    SendResult sent,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default
  ) => operations.WaitForVersion(SpeckleClient.Account, sent, timeout, cancellationToken);

  private void GuardAgainstCircularTrigger(Model model)
  {
    foreach (var trigger in AutomationRunData.Triggers)
    {
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
  }

  /// <summary>
  /// Set context view for automation result view.
  /// </summary>
  /// <param name="resourceIds"> Resource contexts to bind into view.</param>
  /// <param name="includeSourceModelVersion"> Whether bind source version into result view or not.</param>
  /// <exception cref="SpeckleException"> Throws if there is no context to create result view.</exception>
  [MemberNotNull(nameof(ContextView))]
  [AutoInterfaceIgnore] //Ignore so we can explicitly add the MemberNotNull attibute to the interface method
  public void SetContextView(IReadOnlyCollection<string>? resourceIds = null, bool includeSourceModelVersion = true)
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
            linkResources.Add($"{versionCreationTrigger.Payload.ModelId}@{versionCreationTrigger.Payload.VersionId}");
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

    ContextView = $"/projects/{AutomationRunData.ProjectId}/models/{string.Join(",", linkResources)}";
  }

  public async Task ReportRunStatus()
  {
    ObjectResults? objectResults = null;
    if (RunStatus is "SUCCEEDED" or "FAILED")
    {
      objectResults = new ObjectResults
      {
        Version = 2,
        Values = new ObjectResultValues
        {
          BlobIds = AutomationResult.Blobs,
          ObjectResults = AutomationResult.ObjectResults,
        },
      };
    }

    //language=graphql
    const string QUERY = """
      mutation AutomateFunctionRunStatusReport($projectId: String!, $functionRunId: String!, $status: AutomateRunStatus!, $statusMessage: String, $results: JSONObject, $contextView: String) {
        automateFunctionRunStatusReport(
          input: {projectId: $projectId, functionRunId: $functionRunId, status: $status, statusMessage: $statusMessage, contextView: $contextView, results: $results}
        )
      }
      """;
    GraphQLRequest request = new()
    {
      Query = QUERY,
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
    HttpResponseMessage request = await SpeckleClient
      .GQLClient.HttpClient.PostAsync(
        new Uri(AutomationRunData.SpeckleServerUrl, $"api/stream/{AutomationRunData.ProjectId}/blob"),
        formData
      )
      .ConfigureAwait(false);
    request.EnsureSuccessStatusCode();
    string responseString = await request.Content.ReadAsStringAsync().ConfigureAwait(false);

    logger.LogInformation("RESPONSE - {Response}", responseString);
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

    logger.LogInformation("Marking run with {Status} {Message}", status, msg);
  }

  public void MarkRunFailed(string statusMessage) => MarkRun(AutomationStatus.Failed, statusMessage);

  public void MarkRunException(string? statusMessage) => MarkRun(AutomationStatus.Exception, statusMessage);

  public void MarkRunSuccess(string? statusMessage) => MarkRun(AutomationStatus.Succeeded, statusMessage);

  /// <summary>
  /// Add a new error case to the run results.
  /// </summary>
  /// <param name="category">A short tag for the error type.</param>
  /// <param name="affectedObjects">A list of objects that are causing the result.</param>
  /// <param name="message">Optional error message.</param>
  /// <param name="metadata">User provided metadata key value pairs.</param>
  /// <param name="visualOverrides">Case specific 3D visual overrides.</param>
  /// <exception cref="ArgumentException">Throws if the provided <paramref name="affectedObjects"/> input is empty.</exception>
  public void AttachErrorToObjects(
    string category,
    IReadOnlyCollection<Base> affectedObjects,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Error, category, affectedObjects, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new warning case to the run results.
  /// </summary>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachWarningToObjects(
    string category,
    IReadOnlyCollection<Base> affectedObjects,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Warning, category, affectedObjects, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new info case to the run results.
  /// </summary>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachInfoToObjects(
    string category,
    IReadOnlyCollection<Base> affectedObjects,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Info, category, affectedObjects, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new success case to the run results.
  /// </summary>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachSuccessToObjects(
    string category,
    IReadOnlyCollection<Base> affectedObjects,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  ) => AttachResultToObjects(ObjectResultLevel.Success, category, affectedObjects, message, metadata, visualOverrides);

  /// <summary>
  /// Add a new case to the run results.
  /// </summary>
  /// <param name="level">The level assigned to this result.</param>
  /// <inheritdoc cref="AttachErrorToObjects"/>
  public void AttachResultToObjects(
    ObjectResultLevel level,
    string category,
    IReadOnlyCollection<Base> affectedObjects,
    string? message = null,
    Dictionary<string, object>? metadata = null,
    Dictionary<string, object>? visualOverrides = null
  )
  {
    if (affectedObjects.Count == 0)
    {
      throw new ArgumentException($"Need at least one affected object to report a(n) {level}");
    }

    string levelString = ObjectResultLevelMapping.Get(level);
    Dictionary<string, string?> ids = affectedObjects.ToDictionary(
      x => x.id.NotNull($"You can only attach {level} results to objects with an id"),
      x => x.applicationId
    );

    logger.LogInformation(
      "Created new {Level} category: {Category} caused by: {Messag}",
      levelString.ToUpper(),
      category,
      message
    );

    ResultCase resultCase = new()
    {
      Category = category,
      Level = levelString,
      ObjectAppIds = ids,
      Message = message,
      Metadata = metadata,
      VisualOverrides = visualOverrides,
    };

    AutomationResult.ObjectResults.Add(resultCase);
  }
}

public partial interface IAutomationContext
{
  [MemberNotNull(nameof(ContextView))]
  void SetContextView(IReadOnlyCollection<string>? resourceIds = null, bool includeSourceModelVersion = true);
}
