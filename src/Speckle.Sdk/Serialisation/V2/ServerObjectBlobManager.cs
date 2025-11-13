using Speckle.InterfaceGenerator;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class ServerBlobManager : IServerBlobManager
{
  private readonly HttpClient _authorizedClient;

  public ServerBlobManager(HttpClient authorizedClient)
  {
    _authorizedClient = authorizedClient;
  }

  public async Task UploadBlobs(
    string projectId,
    IReadOnlyCollection<(string blobId, string filePath)> objects,
    IProgress<ProgressArgs>? progress,
    CancellationToken cancellationToken
  )
  {
    if (objects.Count == 0)
    {
      return;
    }

    var multipartFormDataContent = new MultipartFormDataContent();
    foreach (var (id, filePath) in objects)
    {
      var fileName = Path.GetFileName(filePath);
      var stream = File.OpenRead(filePath);
      StreamContent fsc = new(stream);
      var hash = id.Split(':')[1];

      multipartFormDataContent.Add(fsc, $"hash:{hash}", fileName);
      cancellationToken.ThrowIfCancellationRequested();
    }

    using var message = new HttpRequestMessage();
    message.RequestUri = new Uri($"/api/stream/{projectId}/blob", UriKind.Relative);
    message.Method = HttpMethod.Post;
    message.Content = new ProgressContent(multipartFormDataContent, progress);

    using var response = await _authorizedClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }
}
