using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Speckle.Sdk.Api;

public readonly struct VersionedModel
{
  public string ModelId { get; init; }
  public string? VersionId { get; init; }
}

//resource = model or model@version or objectid
public sealed partial class SpeckleURL
{
  private static readonly string PROJECTS_SEGMENT = "Projects";
  private static readonly string MODELS_SEGMENT = "Models";

  public Uri? ServerUrl { get; set; }
  public string? UserId { get; set; }
  public string? ProjectId { get; set; }
  public List<VersionedModel> models { get; } = new();
  public List<string> objects { get; } = new();

  public SpeckleURL(Uri? url)
  {
    if (url != null && url.IsAbsoluteUri)
    {
      ServerUrl = new UriBuilder
      {
        Host = url.Host,
        Port = url.Port,
        Scheme = url.Scheme,
      }.Uri;
      TryGetCaptures(url.AbsolutePath);
    }
  }

  private const string MODELS_GROUP = "models";
  private const string PROJECT_ID_GROUP = "projectId";
  
  //language=regex
  private const string URL_REGEX_STRING =
    @"^/projects/(?<projectId>[a-f0-9]{10})/models/(?<models>(?:[a-f0-9]{10}(?:@[a-f0-9]{10})?|[a-f0-9]{32})(?:,\2)*)$";
    // "^/projects/(?<projectId>[a-f0-9]{10})/models/(?<models>(?:[a-f0-9]{10}(?:@[a-f0-9]{10})?|[a-f0-9]{32})(?:,(?:[a-f0-9]{10}(?:@[a-f0-9]{10})?|[a-f0-9]{32}))*)$";
    // "^/projects/(?<projectId>[a-f0-9]{10})/models/((?<models>:[a-f0-9]{32}|[a-f0-9]{10}(?:@[a-f0-9]{10})?)(?:,.*)*)$";


#if NET7_0_OR_GREATER
  [GeneratedRegex(URL_REGEX_STRING)]
  private static partial Regex GenerateUrlRegex();

  private static readonly Regex UrlRegex = GenerateUrlRegex();
#else
  private static readonly Regex UrlRegex = new(URL_REGEX_STRING, );
#endif
  
  private SetFieldsFromPath(string absolutePath)
  {
    var match = UrlRegex.Match(absolutePath);
    var projectId = match.Groups[PROJECT_ID_GROUP];
    var modelIds =  match.Groups[MODELS_GROUP];
    
    if (!projectId.Success)
    {
      throw new ArgumentException("The provided url is not a valid Speckle url", nameof(absolutePath));
    }

    if (!modelIds.Success)
    {
      throw new ArgumentException("The provided url is not pointing to any model in the project", nameof(absolutePath));
    }
    
    

  }



  private void ValidateInputUrl([NotNull] Uri? url)
  {
    if (url is null)
    {
      throw new ArgumentNullException(nameof(url));
    }

    if (!url.IsAbsoluteUri)
    {
      throw new ArgumentException("Expected an absolute Url");
    }
  }

  private void SetFieldsFromUrl(Uri url)
  {
    SetFieldsFromPath(url.Segments);
  }

  public Uri ToUrl()
  {
  }
}
