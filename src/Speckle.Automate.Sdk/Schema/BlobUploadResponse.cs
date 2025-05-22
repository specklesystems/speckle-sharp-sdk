namespace Speckle.Automate.Sdk.Schema;

public readonly struct BlobUploadResponse
{
  public required List<UploadResult> UploadResults { get; init; }
}
