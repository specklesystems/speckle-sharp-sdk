namespace Speckle.Automate.Sdk.Schema;

public readonly struct UploadResult
{
  public required string BlobId { get; init; }
  public required string FileName { get; init; }
  public required int UploadStatus { get; init; }
}
