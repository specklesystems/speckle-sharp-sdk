using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Data.Sqlite;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Performance;

public sealed class TestDataHelper(IAccountManager accountManager, 
  IClientFactory clientFactory, IServerTransportFactory serverTransportFactory, IOperations operations) : IDisposable
{
  private static readonly string s_basePath = $"./temp {Guid.NewGuid()}";

  public SQLiteTransport Transport { get; private set; }
  public string ObjectId { get; private set; }

  public async Task SeedTransport(StreamWrapper sw)
  {
    // Transport = new SQLiteTransport(s_basePath, APPLICATION_NAME);
    Transport = new SQLiteTransport();

    //seed SQLite transport with test data
    ObjectId = await SeedTransport(sw, Transport).ConfigureAwait(false);
  }

  public async Task<string> SeedTransport( StreamWrapper sw, ITransport transport)
  {
    //seed SQLite transport with test data
    var acc =  sw.GetAccount(accountManager);
    using var client = clientFactory.Create(acc);
    var branch = await client.Model.Get(sw.StreamId, sw.BranchName!, default).ConfigureAwait(false);
    var objectId = branch.id;

    using ServerTransport remoteTransport = serverTransportFactory.Create(acc, sw.StreamId);
    transport.BeginWrite();
    await remoteTransport.CopyObjectAndChildren(objectId, transport).ConfigureAwait(false);
    transport.EndWrite();
    await transport.WriteComplete().ConfigureAwait(false);

    return objectId;
  }

  public async Task<Base> DeserializeBase()
  {
    return await operations.Receive(ObjectId, null, Transport).ConfigureAwait(false);
  }

  public void Dispose()
  {
    Transport.Dispose();
    SqliteConnection.ClearAllPools();
    if (Directory.Exists(s_basePath))
    {
      Directory.Delete(s_basePath, true);
    }
  }
}

public class StreamWrapper
{
  private Account? _account;

  private StreamWrapper() { }



  //this needs to be public so it's serialized and stored in Dynamo
  public string? OriginalInput { get; set; }

  public string? UserId { get; set; }
  public string ServerUrl { get; set; }
  public string StreamId { get; set; }
  public string? CommitId { get; set; }

  /// <remarks>May be an ID instead for FE2 urls</remarks>
  public string? BranchName { get; set; }
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

      if (!string.IsNullOrEmpty(BranchName))
      {
        return StreamWrapperType.Branch;
      }

      // If we reach here and there is no stream id, it means that the stream is invalid for some reason.
      return !string.IsNullOrEmpty(StreamId) ? StreamWrapperType.Stream : StreamWrapperType.Undefined;
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
    StreamId = projectId.Value;
    BranchName = modelRes.branchId;
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
  public  StreamWrapper(string streamUrl)
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
      StreamId = uri.Segments[2].Replace("/", "");
      if (uri.Segments.Length > 5)
      {
        var branchSegs = uri.Segments.ToList().GetRange(4, uri.Segments.Length - 4);
        BranchName = Uri.UnescapeDataString(string.Concat(branchSegs));
      }
      else
      {
        BranchName = Uri.UnescapeDataString(uri.Segments[4]);
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
            StreamId = uri.Segments[2].Replace("/", "");
          }

          break;
        case 4: // ie https://speckle.server/streams/0c6ad366c4/globals/
          if (uri.Segments[3].StartsWith("globals", StringComparison.InvariantCultureIgnoreCase))
          {
            StreamId = uri.Segments[2].Replace("/", "");
            BranchName = Uri.UnescapeDataString(uri.Segments[3].Replace("/", ""));
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
              StreamId = uri.Segments[2].Replace("/", "");
              CommitId = uri.Segments[4].Replace("/", "");
              break;
            case "globals/":
              StreamId = uri.Segments[2].Replace("/", "");
              BranchName = Uri.UnescapeDataString(uri.Segments[3].Replace("/", ""));
              CommitId = uri.Segments[4].Replace("/", "");
              break;
            case "branches/":
              StreamId = uri.Segments[2].Replace("/", "");
              BranchName = Uri.UnescapeDataString(uri.Segments[4].Replace("/", ""));
              break;
            case "objects/":
              StreamId = uri.Segments[2].Replace("/", "");
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

  /// <summary>
  /// Gets a valid account for this stream wrapper.
  /// <para>Note: this method ensures that the stream exists and/or that the user has an account which has access to that stream. If used in a sync manner, make sure it's not blocking.</para>
  /// </summary>
  /// <exception cref="SpeckleException">Throws exception if account fetching failed. This could be due to non-existent account or stream.</exception>
  /// <returns>The valid account object for this stream.</returns>
  public Account GetAccount(IAccountManager accountManager)
  {
    if (_account != null)
    {
      return _account;
    }

    // Step 1: check if direct account id (?u=)
    if (OriginalInput != null && OriginalInput.Contains("?u="))
    {
      var userId = OriginalInput.Split(new[] { "?u=" }, StringSplitOptions.None)[1];
      var acc = accountManager.GetAccounts().FirstOrDefault(acc => acc.userInfo.id == userId);
      if (acc != null)
      {
        _account = acc;
        return acc;
      }
    }

    // Step 2: check the default
    var defAcc = accountManager.GetDefaultAccount();
    List<Exception> err = new();
    try
    {
      defAcc.NotNull();
      _account = defAcc;
      return defAcc;
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      err.Add(new SpeckleException($"Account {defAcc?.userInfo?.email} failed to auth stream wrapper", ex));
    }

    // Step 3: all the rest
    var accs = accountManager.GetAccounts(new Uri(ServerUrl)).ToList();
    if (accs.Count == 0)
    {
      throw new SpeckleException($"You don't have any accounts for {ServerUrl}.");
    }

    foreach (var acc in accs)
    {
      try
      {
        _account = acc;
        return acc;
      }
      catch (Exception ex) when (!ex.IsFatal())
      {
        err.Add(new SpeckleException($"Account {acc} failed to auth stream wrapper", ex));
      }
    }

    AggregateException inner = new(null, err);
    throw new SpeckleException("Failed to validate stream wrapper", inner);
  }

  public void SetAccount(Account acc)
  {
    _account = acc;
    UserId = _account.userInfo.id;
  }

  public bool Equals(StreamWrapper? wrapper)
  {
    if (wrapper == null)
    {
      return false;
    }

    if (Type != wrapper.Type)
    {
      return false;
    }

    return Type == wrapper.Type
           && ServerUrl == wrapper.ServerUrl
           && UserId == wrapper.UserId
           && StreamId == wrapper.StreamId
           && Type == StreamWrapperType.Branch
           && BranchName == wrapper.BranchName
           || Type == StreamWrapperType.Object && ObjectId == wrapper.ObjectId
           || Type == StreamWrapperType.Commit && CommitId == wrapper.CommitId;
  }

  public Uri ToServerUri()
  {
    if (_account != null)
    {
      return _account.serverInfo.frontend2 ? ToProjectUri() : ToStreamUri();
    }

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
    var branchID = BranchName;
    var leftPart = $"projects/{StreamId}/models/";
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
    var leftPart = $"streams/{StreamId}";
    switch (Type)
    {
      case StreamWrapperType.Commit:
        leftPart += $"/commits/{CommitId}";
        break;
      case StreamWrapperType.Branch:
        leftPart += $"/branches/{BranchName}";
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
public enum StreamWrapperType
{
  Undefined,
  Stream,
  Commit,
  Branch,
  Object
}
