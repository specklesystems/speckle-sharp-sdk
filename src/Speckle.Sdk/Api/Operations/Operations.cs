using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.V2;

namespace Speckle.Sdk.Api;

/// <summary>
/// Exposes several key methods for interacting with Speckle.Sdk.
/// <para>Serialize/Deserialize</para>
/// <para>Push/Pull (methods to serialize and send data to one or more servers)</para>
/// </summary>
[GenerateAutoInterface]
public partial class Operations(
  ISdkActivityFactory activityFactory,
  ISdkMetricsFactory metricsFactory,
  ISerializeProcessFactory serializeProcessFactory,
  IDeserializeProcessFactory deserializeProcessFactory
) : IOperations;
