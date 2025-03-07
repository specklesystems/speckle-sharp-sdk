using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Inputs;

public sealed record ProjectCommentsFilter(bool? includeArchived, bool? loadedVersionsOnly, string? resourceIdString);

public sealed record ProjectCreateInput(string? name, string? description, ProjectVisibility? visibility);

public sealed record ProjectInviteCreateInput(string? email, string? role, string? serverRole, string? userId);

public sealed record ProjectInviteUseInput(bool accept, string projectId, string token);

public sealed record ProjectModelsFilter(
  IReadOnlyList<string>? contributors = null,
  IReadOnlyList<string>? excludeIds = null,
  IReadOnlyList<string>? ids = null,
  bool? onlyWithVersions = null,
  string? search = null,
  IReadOnlyList<string>? sourceApps = null
);

public sealed record ProjectUpdateInput(
  string id,
  string? name = null,
  string? description = null,
  bool? allowPublicComments = null,
  ProjectVisibility? visibility = null
);

public sealed record ProjectUpdateRoleInput(string userId, string projectId, string? role);

public sealed record UserProjectsFilter(string search, IReadOnlyList<string>? onlyWithRoles = null);
