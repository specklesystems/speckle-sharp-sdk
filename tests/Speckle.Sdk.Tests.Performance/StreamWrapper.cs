using System.Text.RegularExpressions;
using System.Web;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Performance;

public class StreamWrapper
{
  private StreamWrapper() { }

  //this needs to be public so it's serialized and stored in Dynamo
  public string? OriginalInput { get; set; }

  public string? UserId { get; set; }
  public string ServerUrl { get; set; }
  public string ProjectId { get; set; }
  public string? CommitId { get; set; }

  /// <remarks>May be an ID instead for FE2 urls</remarks>
  public string? ModelId { get; set; }
  public string? ObjectId { get; set; }

  /// <summary>
  /// Determines if the current stream wrapper contains a valid stream.
  /// </summary>
  public bool IsValid => Type != StreamWrapperType.Undefined;

  public StreamWrapperType Type
  {
    // Quick solution to determine whether a wrapper points to a branch, commit or stream.
    get
    {
      if (!string.IsNullOrEmpty(ObjectId))
      {
        return StreamWrapperType.Object;
      }

      if (!string.IsNullOrEmpty(CommitId))
      {
        return StreamWrapperType.Commit;
      }

      if (!string.IsNullOrEmpty(ModelId))
      {
        return StreamWrapperType.Branch;
      }

      // If we reach here and there is no stream id, it means that the stream is invalid for some reason.
      return !string.IsNullOrEmpty(ProjectId) ? StreamWrapperType.Stream : StreamWrapperType.Undefined;
    }
  }

  /// <summary>
  /// The ReGex pattern to determine if a URL's AbsolutePath is a Frontend2 URL or not.
  /// This is used in conjunction with <see cref="ParseFe2ModelValue"/> to extract the correct values into the instance.
  /// </summary>
  private static readonly Regex s_fe2UrlRegex =
    new(
      @"/projects/(?<projectId>[\w\d]+)(?:/models/(?<model>[\w\d]+(?:@[\w\d]+)?)(?:,(?<additionalModels>[\w\d]+(?:@[\w\d]+)?))*)?"
    );

  /// <summary>
  /// Parses a FrontEnd2 URL Regex match and assigns it's data to this StreamWrapper instance.
  /// </summary>
  /// <param name="match">A regex match coming from <see cref="s_fe2UrlRegex"/></param>
  /// <exception cref="SpeckleException">Will throw when the URL is not properly formatted.</exception>
  /// <exception cref="NotSupportedException">Will throw when the URL is correct, but is not currently supported by the StreamWrapper class.</exception>
  private void ParseFe2RegexMatch(Match match)
  {
    var projectId = match.Groups["projectId"];
    var model = match.Groups["model"];
    var additionalModels = match.Groups["additionalModels"];

    if (!projectId.Success)
    {
      throw new SpeckleException("The provided url is not a valid Speckle url");
    }

    if (!model.Success)
    {
      throw new SpeckleException("The provided url is not pointing to any model in the project.");
    }

    if (additionalModels.Success || model.Value == "all")
    {
      throw new NotSupportedException("Multi-model urls are not supported yet");
    }
#if NETSTANDARD2_0
    if (model.Value.StartsWith("$"))
#else
    if (model.Value.StartsWith('$'))
#endif
    {
      throw new NotSupportedException("Federation model urls are not supported");
    }

    var modelRes = ParseFe2ModelValue(model.Value);

    // INFO: The Branch endpoint is being updated to fallback to checking a branch ID if no name is found.
    // Assigning the BranchID as the BranchName is a workaround to support FE2 links in the old StreamWrapper.
    // A better solution must be redesigned taking into account all the new Frontend2 URL features.
    ProjectId = projectId.Value;
    ModelId = modelRes.branchId;
    CommitId = modelRes.commitId;
    ObjectId = modelRes.objectId;
  }

  /// <summary>
  /// Parses the segment of the FE2 URL that represents a modelID, modelID@versionID or objectID.
  /// It is meant to parse a single value. If url is multi-model it should be used once per model.
  /// </summary>
  /// <param name="modelValue">The a single value of the model url segment</param>
  /// <returns>A tuple containing the branch, commit and object information for that value. Each value can be null</returns>
  /// <remarks>Determines if a modelValue is an ObjectId by checking it's length is exactly 32 chars long.</remarks>
  private static (string? branchId, string? commitId, string? objectId) ParseFe2ModelValue(string modelValue)
  {
    if (modelValue.Length == 32)
    {
      return (null, null, modelValue); // Model value is an ObjectID
    }

    if (!modelValue.Contains('@'))
    {
      return (modelValue, null, null); // Model has no version attached
    }

    var res = modelValue.Split('@');
    return (res[0], res[1], null); // Model has version attached
  }

#pragma warning disable CA1054
  public StreamWrapper(string streamUrl)
#pragma warning restore CA1054
  {
    Uri uri = new(streamUrl);
    ServerUrl = uri.GetLeftPart(UriPartial.Authority);

    var fe2Match = s_fe2UrlRegex.Match(uri.AbsolutePath);
    if (fe2Match.Success)
    {
      //NEW FRONTEND URL!
      ParseFe2RegexMatch(fe2Match);
      return;
    }

    // Note: this is a hack. It's because new Uri() is parsed escaped in .net framework; wheareas in .netstandard it's not.
    // Tests pass in Speckle.Sdk without this hack.
    if (uri.Segments.Length >= 4 && uri.Segments[3]?.ToLowerInvariant() == "branches/")
    {
      ProjectId = uri.Segments[2].Replace("/", "");
      if (uri.Segments.Length > 5)
      {
        var branchSegs = uri.Segments.ToList().GetRange(4, uri.Segments.Length - 4);
        ModelId = Uri.UnescapeDataString(string.Concat(branchSegs));
      }
      else
      {
        ModelId = Uri.UnescapeDataString(uri.Segments[4]);
      }
    }
    else
    {
      switch (uri.Segments.Length)
      {
        case 3: // ie http://speckle.server/streams/8fecc9aa6d
          if (!uri.Segments[1].Equals("streams/", StringComparison.InvariantCultureIgnoreCase))
          {
            throw new SpeckleException($"Cannot parse {uri} into a stream wrapper class.");
          }
          else
          {
            ProjectId = uri.Segments[2].Replace("/", "");
          }

          break;
        case 4: // ie https://speckle.server/streams/0c6ad366c4/globals/
          if (uri.Segments[3].StartsWith("globals", StringComparison.InvariantCultureIgnoreCase))
          {
            ProjectId = uri.Segments[2].Replace("/", "");
            ModelId = Uri.UnescapeDataString(uri.Segments[3].Replace("/", ""));
          }
          else
          {
            throw new SpeckleException($"Cannot parse {uri} into a stream wrapper class");
          }

          break;
        case 5: // ie http://speckle.server/streams/8fecc9aa6d/commits/76a23d7179
          switch (uri.Segments[3].ToLowerInvariant())
          {
            // NOTE: this is a good practice reminder on how it should work
            case "commits/":
              ProjectId = uri.Segments[2].Replace("/", "");
              CommitId = uri.Segments[4].Replace("/", "");
              break;
            case "globals/":
              ProjectId = uri.Segments[2].Replace("/", "");
              ModelId = Uri.UnescapeDataString(uri.Segments[3].Replace("/", ""));
              CommitId = uri.Segments[4].Replace("/", "");
              break;
            case "branches/":
              ProjectId = uri.Segments[2].Replace("/", "");
              ModelId = Uri.UnescapeDataString(uri.Segments[4].Replace("/", ""));
              break;
            case "objects/":
              ProjectId = uri.Segments[2].Replace("/", "");
              ObjectId = uri.Segments[4].Replace("/", "");
              break;
            default:
              throw new SpeckleException($"Cannot parse {uri} into a stream wrapper class.");
          }

          break;

        default:
          throw new SpeckleException($"Cannot parse {uri} into a stream wrapper class.");
      }
    }

    var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
    UserId = queryDictionary["u"];
  }

  public Uri ToServerUri()
  {
    if (OriginalInput != null)
    {
      Uri uri = new(OriginalInput);
      var fe2Match = s_fe2UrlRegex.Match(uri.AbsolutePath);
      return fe2Match.Success ? ToProjectUri() : ToStreamUri();
    }

    // Default to old FE1
    return ToStreamUri();
  }

  private Uri ToProjectUri()
  {
    var uri = new Uri(ServerUrl);

    // TODO: THis has to be the branch ID or it won't work.
    var branchID = ModelId;
    var leftPart = $"projects/{ProjectId}/models/";
    switch (Type)
    {
      case StreamWrapperType.Commit:
        leftPart += $"{branchID}@{CommitId}";
        break;
      case StreamWrapperType.Branch:
        leftPart += $"{branchID}";
        break;
      case StreamWrapperType.Object:
        leftPart += $"{ObjectId}";
        break;
    }
    var acc = $"{(UserId != null ? "?u=" + UserId : "")}";

    var finalUri = new Uri(uri, leftPart + acc);
    return finalUri;
  }

  private Uri ToStreamUri()
  {
    var uri = new Uri(ServerUrl);
    var leftPart = $"streams/{ProjectId}";
    switch (Type)
    {
      case StreamWrapperType.Commit:
        leftPart += $"/commits/{CommitId}";
        break;
      case StreamWrapperType.Branch:
        leftPart += $"/branches/{ModelId}";
        break;
      case StreamWrapperType.Object:
        leftPart += $"/objects/{ObjectId}";
        break;
    }
    var acc = $"{(UserId != null ? "?u=" + UserId : "")}";

    var finalUri = new Uri(uri, leftPart + acc);
    return finalUri;
  }

  public override string ToString() => ToServerUri().ToString();
}
