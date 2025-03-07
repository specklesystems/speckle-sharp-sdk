namespace Speckle.Sdk.Api.GraphQL.Inputs;

public sealed record UserUpdateInput(
  string? avatar = null,
  string? bio = null,
  string? company = null,
  string? name = null
);
