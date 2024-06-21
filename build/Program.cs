using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string CLEAN = "clean";
const string FORMAT = "format";
const string RESTORE_TOOLS = "restore-tools";

const string RESTORE = "restore";
const string BUILD = "build";
const string TEST = "test";
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

Target(
    RESTORE_TOOLS,
    () =>
    {
        Run("dotnet", "tool restore");
    }
);

Target(
    FORMAT,
    DependsOn(RESTORE_TOOLS),
    () =>
    {
        Run("dotnet", "csharpier --check .");
    }
);

#region 
Target(
    RESTORE,
    () =>
    {
        Run("dotnet", $"restore Speckle.Sdk.sln --locked-mode");
    }
);

Target(
    BUILD,
    DependsOn(RESTORE),
    s =>
    {
        Run("dotnet", $"build Speckle.Sdk.sln -c Release --no-restore");
    }
);

Target(
    TEST,
    DependsOn(BUILD),
    () =>
    {
        IEnumerable<string> GetFiles(string d)
        {
            return Glob.Files(".", d);
        }

        foreach (var file in GetFiles("**/*.Tests.csproj"))
        {
            Run("dotnet", $"test {file} -c Release --no-build --verbosity=normal");
        }
    }
);

Target(
    PACK,
    DependsOn(TEST),
    s =>
    {
        Run("dotnet", $"pack Speckle.Sdk.sln -c Release -o output --no-build");
    }
);
#endregion



Target("default", DependsOn(FORMAT, TEST), () => Console.WriteLine("Done!"));

await RunTargetsAndExitAsync(args).ConfigureAwait(true);
