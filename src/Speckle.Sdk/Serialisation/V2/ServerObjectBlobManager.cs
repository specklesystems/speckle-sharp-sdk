using Speckle.InterfaceGenerator;
using Speckle.Sdk.Transports;
using Speckle.Sdk.Transports.ServerUtils;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface(VisibilityModifier = "public")]
internal sealed class ServerBlobManager(HttpClient authorizedClient, string projectId) : IServerBlobManager
{
  public async Task UploadBlobs(
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

      multipartFormDataContent.Add(fsc, $"hash:{id}", fileName);
      cancellationToken.ThrowIfCancellationRequested();
    }

    using var message = new HttpRequestMessage();
    message.RequestUri = new Uri($"/api/stream/{projectId}/blob", UriKind.Relative);
    message.Method = HttpMethod.Post;
    message.Content = new ProgressContent(multipartFormDataContent, progress);

    using var response = await authorizedClient.SendAsync(message, cancellationToken).ConfigureAwait(false);

    response.EnsureSuccessStatusCode();
  }
}
