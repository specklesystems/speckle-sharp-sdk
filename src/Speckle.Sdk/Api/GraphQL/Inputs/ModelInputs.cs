namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record CreateModelInput(string name, string? description, string projectId);

public record DeleteModelInput(string id, string projectId);

public record UpdateModelInput(string id, string? name, string? description, string projectId);

public record ModelVersionsFilter(IReadOnlyList<string> priorityIds, bool? priorityIdsOnly);
