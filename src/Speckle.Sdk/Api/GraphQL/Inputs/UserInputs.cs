namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record UserUpdateInput(string? avatar = null, string? bio = null, string? company = null, string? name = null);

public record UserProjectsFilter(
  string? search = null,
  IReadOnlyList<string>? onlyWithRoles = null,
  string? workspaceId = null,
  bool? personalOnly = null,
  bool? includeImplicitAccess = null
);

public record UserWorkspacesFilter(string? search);
