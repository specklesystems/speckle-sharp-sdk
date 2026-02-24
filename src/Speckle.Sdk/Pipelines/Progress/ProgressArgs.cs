namespace Speckle.Sdk.Pipelines.Progress;

public readonly record struct CardProgress(string Status, double Progress);

public readonly record struct ConversionProgressArgs(int ObjectsConverted, int TotalObjectsToConvert);
