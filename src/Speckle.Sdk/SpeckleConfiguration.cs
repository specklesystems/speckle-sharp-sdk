using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

public record SpeckleConfiguration(
  HostApplication Application,
  HostAppVersion Version,
  SpeckleLogging? Logging = null,
  SpeckleTracing? Tracing = null
);
