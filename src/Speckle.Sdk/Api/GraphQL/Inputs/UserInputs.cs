namespace Speckle.Sdk.Api.GraphQL.Inputs;

public sealed record UserUpdateInput(
  string? avatar = null,
  string? bio = null,
  string? company = null,
  string? name = null
);

public sealed record UserProjectsFilter(
  string? search = null,
  IReadOnlyList<string>? onlyWithRoles = null,
  string? workspaceId = null,
  bool? personalOnly = null,
  bool? includeImplicitAccess = null
);

public sealed record UserWorkspacesFilter(string? search);
