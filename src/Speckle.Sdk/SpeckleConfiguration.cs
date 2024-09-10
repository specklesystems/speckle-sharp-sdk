using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

public record SpeckleConfiguration(HostApplication Application, HostAppVersion Version, SpeckleTracing? Tracing = null);
