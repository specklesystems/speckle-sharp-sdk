using System.Text.Json;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string CLEAN = "clean";
const string FORMAT = "format";
const string RESTORE_TOOLS = "restore-tools";

const string RESTORE = "restore";
const string BUILD = "build";
const string TEST = "test";
const string INTEGRATION = "integration";
const string PACK = "pack";
const string CLEAN_LOCKS = "clean-locks";
const string PERF = "perf";
const string DEEP_CLEAN = "deep-clean";

static async Task<(string, string)> GetVersions()
{
  var (output, _) = await ReadAsync("dotnet", "dotnet-gitversion /output json").ConfigureAwait(false);
  output = output.Trim();
  var jDoc = JsonDocument.Parse(output);
  var version = jDoc.RootElement.GetProperty("FullSemVer").GetString() ?? "3.0.0-localBuild";
  var fileVersion = jDoc.RootElement.GetProperty("AssemblySemFileVer").GetString() ?? "3.0.0.0";
  return (version, fileVersion);
}

Target(
  CLEAN_LOCKS,
  () =>
  {
    foreach (var f in Glob.Files(".", "**/*.lock.json"))
    {
      Console.WriteLine("Found and will delete: " + f);
      File.Delete(f);
    }
    Console.WriteLine("Running restore now.");
    Run("dotnet", "restore .\\Speckle.Sdk.sln");
  }
);

Target(
  CLEAN,
  ForEach("**/output"),
  dir =>
  {
    IEnumerable<string> GetDirectories(string d)
    {
      return Glob.Directories(".", d);
    }

    void RemoveDirectory(string d)
    {
      if (Directory.Exists(d))
      {
        Console.WriteLine(d);
        Directory.Delete(d, true);
      }
    }

    foreach (var d in GetDirectories(dir))
    {
      RemoveDirectory(d);
    }
  }
);

Target(RESTORE_TOOLS, () => RunAsync("dotnet", "tool restore"));

Target(FORMAT, DependsOn(RESTORE_TOOLS), () => RunAsync("dotnet", "csharpier --check ."));

Target(RESTORE, DependsOn(FORMAT), () => RunAsync("dotnet", "restore Speckle.Sdk.sln --locked-mode"));

Target(
  BUILD,
  DependsOn(RESTORE),
  async () =>
  {
    var (version, fileVersion) = await GetVersions().ConfigureAwait(false);
    Console.WriteLine($"Version: {version} & {fileVersion}");
    await RunAsync(
        "dotnet",
        $"build Speckle.Sdk.sln -c Release --no-restore -warnaserror -p:Version={version} -p:FileVersion={fileVersion}"
      )
      .ConfigureAwait(false);
  }
);

Target(
  TEST,
  DependsOn(BUILD),
  Glob.Files(".", "**/*.Tests.Unit.csproj").Concat(Glob.Files(".", "**/*.Tests.csproj")),
  async file =>
  {
    await RunAsync(
        "dotnet",
        $"test {file} -c Release --no-build --no-restore --verbosity=normal  /p:AltCover=true  /p:AltCoverAttributeFilter=ExcludeFromCodeCoverage /p:AltCoverVerbosity=Warning"
      )
      .ConfigureAwait(false);
  }
);

Target(
  INTEGRATION,
  DependsOn(BUILD),
  async () =>
  {
    await RunAsync("docker", "compose -f docker-compose.yml up --wait").ConfigureAwait(false);
    foreach (var test in Glob.Files(".", "**/*.Tests.Integration.csproj"))
    {
      await RunAsync(
          "dotnet",
          $"test {test} -c Release --no-build --no-restore --verbosity=normal  /p:AltCover=true  /p:AltCoverAttributeFilter=ExcludeFromCodeCoverage"
        )
        .ConfigureAwait(false);
    }
    await RunAsync("docker", "compose down").ConfigureAwait(false);
  }
);

Target(
  PERF,
  Glob.Files(".", "**/*.Tests.Performance.csproj"),
  async file =>
  {
    void CheckBuildDirectory(string dir, string build)
    {
      var binDir = Path.Combine(dir, "bin", build);
      Console.WriteLine($"Checking: {binDir}");
      if (Directory.Exists(binDir))
      {
        Directory.Delete(binDir, true);
        Console.WriteLine($"Deleted: {binDir}");
      }
    }
    var dir = Path.GetDirectoryName(file) ?? throw new InvalidOperationException();
    CheckBuildDirectory(dir, "Release");
    CheckBuildDirectory(dir, "Debug");
    await RunAsync("dotnet", $"run --project {file} -c Release").ConfigureAwait(false);
  }
);

Target(
  DEEP_CLEAN,
  () =>
  {
    foreach (var f in Glob.Directories(".", "**/bin"))
    {
      if (f.StartsWith("build"))
      {
        continue;
      }
      Console.WriteLine("Found and will delete: " + f);
      Directory.Delete(f, true);
    }
    foreach (var f in Glob.Directories(".", "**/obj"))
    {
      if (f.StartsWith("Build"))
      {
        continue;
      }
      Console.WriteLine("Found and will delete: " + f);
      Directory.Delete(f, true);
    }
    Console.WriteLine("Running restore now.");
    Run("dotnet", "restore .\\Speckle.Sdk.sln --no-cache");
  }
);

Target(
  PACK,
  DependsOn(BUILD),
  async () =>
  {
    {
      var (version, fileVersion) = await GetVersions().ConfigureAwait(false);
      Console.WriteLine($"Version: {version} & {fileVersion}");
      await RunAsync("dotnet", $"pack Speckle.Sdk.sln -c Release -o output --no-build -p:Version={version}")
        .ConfigureAwait(false);
    }
  }
);

Target("default", DependsOn(FORMAT, TEST, INTEGRATION), () => Console.WriteLine("Done!"));

await RunTargetsAndExitAsync(args).ConfigureAwait(true);
