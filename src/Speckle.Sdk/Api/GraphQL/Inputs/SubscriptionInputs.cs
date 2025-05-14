namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record ViewerUpdateTrackingTarget(string projectId, string resourceIdString, bool? loadedVersionsOnly = null);
