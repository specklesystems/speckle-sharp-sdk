using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class UserProjectsUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required UserProjectsUpdatedMessageType type { get; init; }

  public Project? project { get; init; }
}

public sealed class ProjectCommentsUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required ProjectCommentsUpdatedMessageType type { get; init; }

  public Comment? comment { get; init; }
}

public sealed class ProjectFileImportUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required ProjectFileImportUpdatedMessageType type { get; init; }

  public FileUpload? upload { get; init; }
}

public sealed class ProjectModelsUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required ProjectModelsUpdatedMessageType type { get; init; }

  public Model? model { get; init; }
}

public sealed class ProjectPendingModelsUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required ProjectPendingModelsUpdatedMessageType type { get; init; }

  public FileUpload? model { get; init; }
}

public sealed class ProjectUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required ProjectUpdatedMessageType type { get; init; }

  public Project? project { get; init; }
}

public sealed class ProjectVersionsUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required string id { get; init; }

  [JsonRequired]
  public required ProjectVersionsUpdatedMessageType type { get; init; }

  [JsonRequired]
  public required string modelId { get; init; }

  public Version? version { get; init; }
}

public sealed class ProjectModelIngestionUpdatedMessage : EventArgs
{
  [JsonRequired]
  public required ModelIngestion modelIngestion { get; init; }

  [JsonRequired]
  public required ProjectModelIngestionUpdatedMessageType type { get; init; }
}
