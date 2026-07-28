using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Artifacts.Harness;
using Speckle.Sdk.Artifacts.Harness.Logging;
using Speckle.Sdk.Artifacts.Harness.Migration;
using Speckle.Sdk.Logging;

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
const string SLUG = "artefact-harness";

// ── init the Speckle type registry (so the deserializer yields TYPED proxies/meshes) ──
services.AddSpeckleSdk(new("ArtefactHarness", SLUG), "v3", typeof(Mesh).Assembly);

// AddSpeckleSdk wires the logging infrastructure but no output provider; add a console sink here so the
// harness' log output is visible (harness-only — the SDK is left untouched).
services.AddTransient<RemoteSource>();
services.AddSingleton<ArtifactHelper>();
services.AddSingleton<GraphArtifactProducerFactory>();
services.AddTransient<BundleUploader>();
services.AddTransient<BundleMigrationClient>();
services.AddTransient<SgeoSelfTest>();
services.AddTransient<Harness>();
services.AddTransient<HarnessCommandLine>();
services.AddSingleton<ISdkActivityFactory, SdkActivityFactory>();
services.AddOTelLogging(SLUG);

await using var serviceProvider = services.BuildServiceProvider(
  options: new() { ValidateOnBuild = true, ValidateScopes = true }
);

var commandLine = serviceProvider.GetRequiredService<HarnessCommandLine>();
var activityFactory = serviceProvider.GetRequiredService<ISdkActivityFactory>();

using var activity = activityFactory.StartActivityFromEnv();
try
{
  int ret = await commandLine.Build().Parse(args).InvokeAsync().ConfigureAwait(false);
  activity?.SetStatus(SdkActivityStatusCode.Ok);
  return ret;
}
catch (Exception ex)
{
  activity?.RecordException(ex);
  activity?.SetStatus(SdkActivityStatusCode.Error);
  throw;
}
