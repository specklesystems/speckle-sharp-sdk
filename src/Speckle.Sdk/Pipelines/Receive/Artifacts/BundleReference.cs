using System.Diagnostics.CodeAnalysis;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// The value a bundle-only (Speckle 2026.9.0) version carries in <c>Version.referencedObject</c> in place of an object
/// hash: <c>bundle.&lt;projectId&gt;.&lt;modelId&gt;.&lt;versionId&gt;</c>. The contract is server-owned (atlas spec
/// <c>2026-08-big-truck-dev-compat</c> §1); the SDK hard-codes the prefix and dispatches on it at the top of
/// <see cref="Api.Operations.Receive2"/>. The leading token is non-hex by construction, so a bundle reference can
/// never collide with a content hash, and the full triple is carried so the value is self-describing wherever it
/// leaks (webhooks, legacy <c>Commit.referencedObject</c>).
/// </summary>
public sealed record BundleReference(string ProjectId, string ModelId, string VersionId)
{
  public const string PREFIX = "bundle.";
  private const char SEPARATOR = '.';

  /// <summary>Cheap pre-check: does this id look like a bundle reference at all (regardless of validity)?</summary>
  public static bool IsBundleReference(string? id) => id is not null && id.StartsWith(PREFIX, StringComparison.Ordinal);

  /// <summary>
  /// Parses <c>bundle.&lt;projectId&gt;.&lt;modelId&gt;.&lt;versionId&gt;</c>. Returns false for anything that is not a
  /// bundle reference (a plain object hash). Throws for something that claims to be one but is malformed — that is a
  /// server/SDK mismatch the caller must see, never a silent fall-through to the legacy path.
  /// </summary>
  /// <exception cref="SpeckleException">Malformed reference.</exception>
  public static bool TryParse(string? id, [NotNullWhen(true)] out BundleReference? reference)
  {
    reference = null;
    if (!IsBundleReference(id))
    {
      return false;
    }

    // Speckle ids never contain '.', so a plain split is unambiguous.
    string[] parts = id!.Split(SEPARATOR);
    if (parts.Length != 4 || parts.Skip(1).Any(string.IsNullOrEmpty))
    {
      throw new SpeckleException(
        $"'{id}' looks like a bundle reference but is malformed. Expected '{PREFIX}<projectId>.<modelId>.<versionId>'."
      );
    }

    reference = new BundleReference(parts[1], parts[2], parts[3]);
    return true;
  }

  public override string ToString() => $"{PREFIX}{ProjectId}{SEPARATOR}{ModelId}{SEPARATOR}{VersionId}";
}
