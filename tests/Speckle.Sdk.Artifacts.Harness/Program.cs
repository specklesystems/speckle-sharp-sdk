using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Artifacts.Harness;

// ── CLI ───────────────────────────────────────────────────────────────────────────────
// INPUT (pick one):
//   --local <ndjsonPath> [--root <id|auto>]
//   --remote <serverUrl> <projectId> <modelId> [--version <versionId>]   (token: SPECKLE_SRC_TOKEN)
// OUTPUT:
//   --out <dir>                                  (default: temp dir)
//   --upload <serverUrl> <projectId> <modelId>   (token: SPECKLE_DST_TOKEN)
// Both --out and --upload may apply. --upload implies a temp dir if --out is absent.
//
// Backwards-compat: if the first arg is not a recognised flag, falls back to the legacy
// positional form `<ndjsonPath> [rootId|auto] [outDir]`.
//
// Program.cs is intentionally thin: register the DI container, resolve the Harness, run it.
// All business logic lives in Harness.Execute.

var services = new ServiceCollection();

// ── init the Speckle type registry (so the deserializer yields TYPED proxies/meshes) ──
services.AddSpeckleSdk(new("ArtefactHarness", "artefact-harness"), "v3", typeof(Mesh).Assembly);
services.AddTransient<RemoteSource>();
services.AddSingleton<GraphArtifactProducer2Factory>();
services.AddTransient<Harness>();

await using var serviceProvider = services.BuildServiceProvider();

var harness = serviceProvider.GetRequiredService<Harness>();
return await harness.Execute(args).ConfigureAwait(false);
