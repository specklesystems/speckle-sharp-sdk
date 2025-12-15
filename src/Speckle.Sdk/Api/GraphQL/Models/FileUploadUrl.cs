namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class FileUploadUrl
{
  public required Uri url { get; init; }
  public required string fileId { get; init; }
}
