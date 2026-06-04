#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Pipelines.Receive;

[JsonSourceGenerationOptions(
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
  GenerationMode = JsonSourceGenerationMode.Metadata
)]
[JsonSerializable(typeof(LightWeightDataChunk<double>))]
[JsonSerializable(typeof(LightWeightDataChunk<int>))]
[JsonSerializable(typeof(LightWeightObjectReference))]
internal partial class SpeckleJsonContext : JsonSerializerContext { }

#endif
