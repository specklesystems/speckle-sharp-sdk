namespace Speckle.Sdk.Pipelines.Progress;

//TODO: rename PipelineProgressArgs
public readonly record struct CardProgress(string Status, double? Progress);

public readonly record struct StreamProgressArgs(long BytesStreamed, long ExpectedTotalBytes);
