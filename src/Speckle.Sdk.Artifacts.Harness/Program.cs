using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Artifacts.Harness;

// ── CLI ───────────────────────────────────────────────────────────────────────────────
// Subcommands (run with --help for full usage):
//   selftest
//   packfile <packfilePath> [--root <id>] [--out <dir>] [--upload <serverUrl> <projectId> <modelId>]
//   remote   <serverUrl> <projectId> <modelId> [versionId] [--dest-* ...] [--legacy-api] [--out <dir>]
// Tokens: SPECKLE_TOKEN (in-place migration), SPECKLE_SRC_TOKEN (remote source), SPECKLE_DST_TOKEN (upload).
//
// Program.cs is intentionally thin: register the DI container, build the command tree, invoke it.
// The parsing lives in HarnessCommandLine; the business logic lives in Harness.

var services = new ServiceCollection();

// ── init the Speckle type registry (so the deserializer yields TYPED proxies/meshes) ──
services.AddSpeckleSdk(new("ArtefactHarness", "artefact-harness"), "v3", typeof(Mesh).Assembly);

// AddSpeckleSdk wires the logging infrastructure but no output provider; add a console sink here so the
// harness' log output is visible (harness-only — the SDK is left untouched).
services.AddLogging(builder => builder.AddSimpleConsole(o => o.SingleLine = true));
services.AddTransient<RemoteSource>();
services.AddSingleton<ArtifactHelper>();
services.AddSingleton<GraphArtifactProducerFactory>();
services.AddTransient<BundleUploader>();
services.AddTransient<BundleMigrationClient>();
services.AddTransient<SgeoSelfTest>();
services.AddTransient<Harness>();

await using var serviceProvider = services.BuildServiceProvider();

return await HarnessCommandLine.Build(serviceProvider).Parse(args).InvokeAsync().ConfigureAwait(false);
