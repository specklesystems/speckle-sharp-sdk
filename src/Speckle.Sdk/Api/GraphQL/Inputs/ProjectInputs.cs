using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Inputs;

public record ProjectCommentsFilter(bool? includeArchived, bool? loadedVersionsOnly, string? resourceIdString);

public record ProjectCreateInput(string? name, string? description, ProjectVisibility? visibility);

public record WorkspaceProjectCreateInput(
  string? name,
  string? description,
  ProjectVisibility? visibility,
  string workspaceId
);

public record ProjectInviteCreateInput(string? email, string? role, string? serverRole, string? userId);

public record ProjectInviteUseInput(bool accept, string projectId, string token);

public record ProjectModelsFilter(
  IReadOnlyList<string>? contributors = null,
  IReadOnlyList<string>? excludeIds = null,
  IReadOnlyList<string>? ids = null,
  bool? onlyWithVersions = null,
  string? search = null,
  IReadOnlyList<string>? sourceApps = null
);

public record ProjectUpdateInput(
  string id,
  string? name = null,
  string? description = null,
  bool? allowPublicComments = null,
  ProjectVisibility? visibility = null
);

public record ProjectUpdateRoleInput(string userId, string projectId, string? role);

public record WorkspaceProjectsFilter(string? search, bool? withProjectRoleOnly);
