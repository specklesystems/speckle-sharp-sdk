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

Target(RESTORE, () => RunAsync("dotnet", "restore Speckle.Sdk.sln --locked-mode"));

Target(BUILD, DependsOn(RESTORE), () => RunAsync("dotnet", "build Speckle.Sdk.sln -c Release --no-restore"));

Target(
  TEST,
  DependsOn(BUILD),
  Glob.Files(".", "**/*.Tests.Unit.csproj").Concat(Glob.Files(".", "**/*.Tests.csproj")),
  async file =>
  {
    await RunAsync(
      "dotnet",
      $"test {file} -c Release --no-build --no-restore --verbosity=normal  /p:AltCover=true  /p:AltCoverAttributeFilter=ExcludeFromCodeCoverage"
    );
  }
);

Target(
  INTEGRATION,
  DependsOn(BUILD),
  async () =>
  {
    await RunAsync("docker", "compose -f docker-compose.yml up --wait");
    foreach (var test in Glob.Files(".", "**/*.Tests.Integration.csproj").Concat(Glob.Files(".", "**/*.Tests.csproj")))
    {
      await RunAsync(
        "dotnet",
        $"test {test} -c Release --no-build --no-restore --verbosity=normal  /p:AltCover=true  /p:AltCoverAttributeFilter=ExcludeFromCodeCoverage"
      );
    }

    await RunAsync("docker", "compose down");
  }
);

Target(PACK, DependsOn(TEST), () => RunAsync("dotnet", "pack Speckle.Sdk.sln -c Release -o output --no-build"));

Target("default", DependsOn(FORMAT, TEST, INTEGRATION), () => Console.WriteLine("Done!"));

await RunTargetsAndExitAsync(args).ConfigureAwait(true);
