namespace Speckle.Sdk.Bundles;

/// <summary>
/// A parsed Speckle model url: <c>{server}/projects/{projectId}/models/{modelId}[@{versionId}]</c>. The form the web
/// app hands out and users paste; <see cref="Api.Operations.Receive3(Speckle.Sdk.Credentials.Account, Uri, ReceiveOptions?, CancellationToken)"/>
/// accepts it directly.
/// </summary>
public sealed record ModelUrl(Uri Server, string ProjectId, string ModelId, string? VersionId)
{
  /// <summary>True when the url pins a version (<c>…/models/{modelId}@{versionId}</c>); false means "latest".</summary>
  public bool HasVersion => VersionId is not null;

  /// <exception cref="ArgumentException">Not a model url.</exception>
  public static ModelUrl Parse(Uri url)
  {
    if (!TryParse(url, out var parsed))
    {
      throw new ArgumentException(
        $"'{url}' is not a Speckle model url. Expected {{server}}/projects/{{projectId}}/models/{{modelId}}[@{{versionId}}].",
        nameof(url)
      );
    }
    return parsed;
  }

  /// <inheritdoc cref="Parse(Uri)"/>
  public static ModelUrl Parse(string url) =>
    Uri.TryCreate(url, UriKind.Absolute, out var uri)
      ? Parse(uri)
      : throw new ArgumentException($"'{url}' is not an absolute url.", nameof(url));

  public static bool TryParse(string? url, out ModelUrl parsed)
  {
    parsed = null!;
    return !string.IsNullOrWhiteSpace(url)
      && Uri.TryCreate(url, UriKind.Absolute, out var uri)
      && TryParse(uri, out parsed);
  }

  public static bool TryParse(Uri uri, out ModelUrl parsed)
  {
    parsed = null!;
    if (!uri.IsAbsoluteUri)
    {
      return false;
    }
    string[] segments = uri.AbsolutePath.Trim('/').Split('/');
    int p = Array.IndexOf(segments, "projects");
    int m = Array.IndexOf(segments, "models");
    if (p < 0 || m < 0 || m < p || p + 1 >= segments.Length || m + 1 >= segments.Length)
    {
      return false;
    }

    string modelPart = segments[m + 1];
    string? versionId = null;
    int at = modelPart.IndexOf('@');
    if (at >= 0)
    {
      versionId = modelPart.Substring(at + 1);
      modelPart = modelPart.Substring(0, at);
    }
    // A model url can name several models ("a,b,c"); this api receives one.
    if (modelPart.Length == 0 || modelPart.Contains(',') || versionId is { Length: 0 })
    {
      return false;
    }

    parsed = new ModelUrl(new Uri(uri.GetLeftPart(UriPartial.Authority)), segments[p + 1], modelPart, versionId);
    return true;
  }

  public override string ToString() =>
    $"{Server.ToString().TrimEnd('/')}/projects/{ProjectId}/models/{ModelId}{(VersionId is null ? "" : "@" + VersionId)}";
}
