namespace Speckle.Sdk.Bundles;

/// <summary>Options for <see cref="Api.Operations.Send3(Uri, string, string, BundleBuilder, string?, SendOptions?, CancellationToken)"/>.</summary>
/// <param name="Message">Progress / version message shown while the server ingests.</param>
/// <param name="FileName">The source file the bundle came from, if any — recorded on the ingestion.</param>
/// <param name="FileSizeBytes">Its size, if known.</param>
/// <param name="MaxIdleTimeoutSeconds">How long the server keeps the ingestion open without progress before failing it.</param>
/// <param name="KeepFiles">Leave the bundle files on disk after upload (default deletes the builder's directory).</param>
public sealed record SendOptions(
  string? Message = null,
  string? FileName = null,
  long? FileSizeBytes = null,
  int MaxIdleTimeoutSeconds = 600,
  bool KeepFiles = false
)
{
  public static readonly SendOptions Default = new();
}

/// <summary>What a send produced. The version exists on the server once the ingestion completes — the
/// <see cref="IngestionId"/> is what to subscribe to for that; <see cref="VersionId"/> is its pre-allocated id.</summary>
public sealed record SendResult(string ProjectId, string ModelId, string VersionId, string IngestionId, int ObjectCount)
{
  /// <summary>The value the version carries in <c>referencedObject</c>: the bundle reference.</summary>
  public string BundleReference =>
    new Pipelines.Receive.Artifacts.BundleReference(ProjectId, ModelId, VersionId).ToString();
}
