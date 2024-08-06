using Speckle.Core.Kits;
using Speckle.Logging;

namespace Speckle.Core.Logging;

public record SpeckleConfiguration(
  HostApplication Application,
  HostAppVersion Version,
  SpeckleLogging? Logging = null,
  SpeckleTracing? Tracing = null
);
