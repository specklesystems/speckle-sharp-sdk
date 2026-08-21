using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.BundleMigrator;
using Speckle.Sdk.BundleMigrator.Logging;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;

// ── CLI ───────────────────────────────────────────────────────────────────────────────
// Subcommands (run with --help for full usage):
//   selftest
//   packfile <packfilePath> [--root <id>] [--out <dir>] [--upload <serverUrl> <projectId> <modelId>]
//   remote   <serverUrl> <projectId> <modelId> [versionId] [--dest-* ...] [--legacy-api] [--out <dir>]
// Tokens: SPECKLE_TOKEN (in-place migration), SPECKLE_SRC_TOKEN (remote source), SPECKLE_DST_TOKEN (upload).
//
// Program.cs is intentionally thin: register the DI container, build the command tree, invoke it.
// The parsing lives in MigratorCommandLine; the business logic lives in Migrator.

var services = new ServiceCollection();
const string SLUG = "BundleMigrator";

// ── init the Speckle type registry (so the deserializer yields TYPED proxies/meshes) ──
// Both versions land in every produced bundle's meta (producer_version / sdk_version), so both are passed
// explicitly as informational versions: the SDK's own default is the truncated 4-part assembly version, while
// the informational one carries the real semver (pre-release label included).
services.AddSpeckleSdk(
  new("BundleMigrator", SLUG),
  LoggingConfiguration.GetPackageVersion(Assembly.GetExecutingAssembly()) ?? "unknown",
  LoggingConfiguration.GetPackageVersion(typeof(Base).Assembly),
  typeof(Mesh).Assembly
);

// AddSpeckleSdk wires the logging infrastructure but no output provider; add a console sink here so the
// migrator' log output is visible (migrator-only — the SDK is left untouched).
services.AddTransient<RemoteSource>();
services.AddSingleton<ArtifactHelper>();
services.AddSingleton<GraphArtifactProducerFactory>();
services.AddTransient<BundleUploader>();
services.AddTransient<BundleMigrationClient>();
services.AddTransient<SgeoSelfTest>();
services.AddTransient<Migrator>();
services.AddTransient<MigratorCommandLine>();
services.AddSingleton<ISdkActivityFactory, SdkActivityFactory>();
using IDisposable loggingFlush = services.AddOTelLogging(SLUG);

await using var serviceProvider = services.BuildServiceProvider(
  options: new() { ValidateOnBuild = true, ValidateScopes = true }
);

var commandLine = serviceProvider.GetRequiredService<MigratorCommandLine>();
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
