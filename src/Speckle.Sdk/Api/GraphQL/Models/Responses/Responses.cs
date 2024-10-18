using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL.Models.Responses;

// This file holds simple records that represent the root GraphQL response data
// For this reason, we're keeping them internal, allowing us to be flexible without the concern for breaking.
// It also exposes fewer similarly named types to dependent assemblies

[Obsolete]
internal record ProjectResponse([property: JsonRequired] Project project);

[Obsolete]
internal record ActiveUserResponse(User? activeUser);

[Obsolete]
internal record LimitedUserResponse(LimitedUser? otherUser);

internal record ServerInfoResponse([property: JsonRequired] ServerInfo serverInfo);

[Obsolete]
internal record ProjectMutationResponse([property: JsonRequired] ProjectMutation projectMutations);

[Obsolete]
internal record ModelMutationResponse([property: JsonRequired] ModelMutation modelMutations);

[Obsolete]
internal record VersionMutationResponse([property: JsonRequired] VersionMutation versionMutations);

[Obsolete]
internal record ProjectInviteResponse(PendingStreamCollaborator? projectInvite);

[Obsolete]
internal record UserSearchResponse([property: JsonRequired] ResourceCollection<LimitedUser> userSearch);

//All of the above records could be replaced by either RequiredResponse or OptionalResponse, if we use an alias (see https://www.baeldung.com/graphql-field-name)
internal record RequiredResponse<T>([property: JsonRequired] T data);

[JsonObject(ItemRequired = Required.AllowNull)]
internal record OptionalResponse<T>(T? data);
