using Speckle.Logging;

namespace Speckle.Core.Logging;

public record SpeckleConfiguration(
  string Application,
  string Version,
  string Slug,
  SpeckleLogging? Logging = null,
  SpeckleTracing? Tracing = null
);
