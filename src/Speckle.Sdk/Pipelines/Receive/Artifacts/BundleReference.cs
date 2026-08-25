using System.Diagnostics.CodeAnalysis;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// The value a bundle-only (Speckle 2026.9.0) version carries in <c>Version.referencedObject</c> in place of an object
/// hash: <c>bundle.1.&lt;projectId&gt;.&lt;modelId&gt;.&lt;versionId&gt;</c>. The contract is server-owned (atlas spec
/// <c>2026-08-big-truck-dev-compat</c> §1); the SDK hard-codes the prefix and dispatches on it at the top of
/// <see cref="Api.Operations.Receive2"/>. The leading token is non-hex by construction, so a bundle reference can
/// never collide with a content hash, and the full triple is carried so the value is self-describing wherever it
/// leaks (webhooks, legacy <c>Commit.referencedObject</c>).
/// </summary>
public sealed record BundleReference(int ContractVersion, string ProjectId, string ModelId, string VersionId)
{
  public const string PREFIX = "bundle.";
  private const char SEPARATOR = '.';

  /// <summary>The only contract version this SDK understands. A higher version fails loud in <see cref="TryParse"/>.</summary>
  public const int SUPPORTED_CONTRACT_VERSION = 1;

  /// <summary>Cheap pre-check: does this id look like a bundle reference at all (regardless of validity)?</summary>
  public static bool IsBundleReference(string? id) => id is not null && id.StartsWith(PREFIX, StringComparison.Ordinal);

  /// <summary>
  /// Parses <c>bundle.1.&lt;projectId&gt;.&lt;modelId&gt;.&lt;versionId&gt;</c>. Returns false for anything that is
  /// not a bundle reference (a plain object hash). Throws for something that claims to be one but is malformed or
  /// carries a contract version this SDK doesn't know — that is a server/SDK mismatch the caller must see.
  /// </summary>
  /// <exception cref="SpeckleException">Malformed reference or unsupported contract version.</exception>
  public static bool TryParse(string? id, [NotNullWhen(true)] out BundleReference? reference)
  {
    reference = null;
    if (!IsBundleReference(id))
    {
      return false;
    }

    // Speckle ids never contain '.', so a plain split is unambiguous.
    string[] parts = id!.Split(SEPARATOR);
    if (parts.Length != 5 || parts.Skip(1).Any(string.IsNullOrEmpty))
    {
      throw new SpeckleException(
        $"'{id}' looks like a bundle reference but is malformed. Expected '{PREFIX}<contractVersion>.<projectId>.<modelId>.<versionId>'."
      );
    }

    if (!int.TryParse(parts[1], out int contractVersion))
    {
      throw new SpeckleException($"'{id}' has a non-numeric bundle contract version '{parts[1]}'.");
    }

    if (contractVersion > SUPPORTED_CONTRACT_VERSION)
    {
      throw new SpeckleException(
        $"'{id}' uses bundle contract version {contractVersion}, but this Speckle.Sdk only understands up to "
          + $"{SUPPORTED_CONTRACT_VERSION}. Upgrade Speckle.Sdk to receive this version."
      );
    }

    reference = new BundleReference(contractVersion, parts[2], parts[3], parts[4]);
    return true;
  }

  public override string ToString() =>
    $"{PREFIX}{ContractVersion}{SEPARATOR}{ProjectId}{SEPARATOR}{ModelId}{SEPARATOR}{VersionId}";
}
