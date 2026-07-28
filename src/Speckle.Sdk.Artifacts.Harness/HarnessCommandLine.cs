using System.CommandLine;
using System.CommandLine.Parsing;
using Speckle.Sdk.Artifacts.Harness.Logging;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Builds the harness command tree (System.CommandLine 2.0.0 GA). Each leaf action calls a typed
/// <see cref="Harness"/> entry point; the action's exit code becomes the process exit code.
/// </summary>
internal sealed class HarnessCommandLine(Harness harness, SgeoSelfTest selfTest, ISdkActivityFactory activityFactory)
{
  public RootCommand Build()
  {
    using var activity = activityFactory.StartActivityFromEnv();
    try
    {
      RootCommand root = new("Speckle artefact-bundle migration harness.");
      root.Subcommands.Add(BuildSelfTest());
      root.Subcommands.Add(BuildPackfile());
      root.Subcommands.Add(BuildRemote());
      activity?.SetStatus(SdkActivityStatusCode.Ok);
      return root;
    }
    catch (Exception ex)
    {
      activity?.RecordException(ex);
      activity?.SetStatus(SdkActivityStatusCode.Error);
      throw;
    }
  }

  private Command BuildSelfTest()
  {
    Command cmd = new("selftest", "Run the SGEO encoder byte-layout self-test.");
    cmd.SetAction(_ => selfTest.Run());
    return cmd;
  }

  private Command BuildPackfile()
  {
    Argument<FileInfo> packfile = new("packfilePath")
    {
      Description = "Path to the DuckDB packfile.",
      CustomParser = ParseExistingFile,
    };
    Option<string> rootOption = new("--root") { Description = "Root object id (default: the packfile's root table)." };
    Option<string> outOption = OutOption();
    Option<string[]> uploadOption = UploadOption();

    Command cmd = new("packfile", "Migrate a graph from a DuckDB packfile.");
    cmd.Arguments.Add(packfile);
    cmd.Options.Add(rootOption);
    cmd.Options.Add(outOption);
    cmd.Options.Add(uploadOption);
    cmd.SetAction(
      async (parseResult, ct) =>
        await harness
          .RunPackfile(
            parseResult.GetRequiredValue(packfile),
            parseResult.GetValue(rootOption),
            parseResult.GetValue(outOption),
            parseResult.GetValue(uploadOption),
            ct
          )
          .ConfigureAwait(false)
    );
    return cmd;
  }

  private Command BuildRemote()
  {
    Argument<Uri> server = new("serverUrl")
    {
      Description = "Source Speckle server URL.",
      CustomParser = ParseAbsoluteUri,
    };
    Argument<string> project = new("projectId") { Description = "Source project id." };
    Argument<string> model = new("modelId") { Description = "Source model id." };
    Argument<string?> version = new("versionId")
    {
      Description = "Source version id (default: latest).",
      Arity = ArgumentArity.ZeroOrOne,
    };
    Option<Uri> destServerOption = new("--dest-server")
    {
      Description = "Destination server URL (default: source server).",
      CustomParser = ParseAbsoluteUri,
    };
    Option<string> destProjectOption = new("--dest-project")
    {
      Description = "Destination project id (default: source).",
    };
    Option<string> destModelOption = new("--dest-model") { Description = "Destination model id (default: source)." };
    Option<bool> legacyApiOption = new("--legacy-api")
    {
      Description = "Fetch the source graph via the REST deserialize API instead of downloading its DuckDB packfile.",
    };
    Option<string> outOption = OutOption();

    Command cmd = new(
      "remote",
      "Migrate a server version. With no --dest-* the bundle is uploaded onto that same version, in place "
        + "(token: SPECKLE_TOKEN); with --dest-* it is uploaded as a new version there "
        + "(tokens: SPECKLE_SRC_TOKEN to read, SPECKLE_DST_TOKEN to upload)."
    );
    cmd.Arguments.Add(server);
    cmd.Arguments.Add(project);
    cmd.Arguments.Add(model);
    cmd.Arguments.Add(version);
    cmd.Options.Add(destServerOption);
    cmd.Options.Add(destProjectOption);
    cmd.Options.Add(destModelOption);
    cmd.Options.Add(legacyApiOption);
    cmd.Options.Add(outOption);

    // Destination is all-or-nothing: either take every part from the source, or specify all three.
    // No destination at all means an IN-PLACE migration of the source version, which pins what else is legal.
    cmd.Validators.Add(result =>
    {
      var specified =
        (result.GetResult(destServerOption) is not null ? 1 : 0)
        + (result.GetResult(destProjectOption) is not null ? 1 : 0)
        + (result.GetResult(destModelOption) is not null ? 1 : 0);
      if (specified is not (0 or 3))
      {
        result.AddError("--dest-server, --dest-project and --dest-model must be specified together (all or none).");
        return;
      }
      if (specified is 3)
      {
        return;
      }

      // In-place: the target version must be explicit (there is no GraphQL access to resolve 'latest'
      // under a migration token), and the legacy fetch needs an api the migration token cannot reach.
      if (result.GetResult(version) is null)
      {
        result.AddError(
          "versionId is required for an in-place migration; pass a versionId, or "
            + "--dest-server/--dest-project/--dest-model to create a new version."
        );
      }
      if (result.GetResult(legacyApiOption) is not null)
      {
        result.AddError(
          "--legacy-api cannot be used for an in-place migration; it needs a user-scoped api, "
            + "so it only applies when creating a new version via --dest-*."
        );
      }
    });

    cmd.SetAction(
      async (parseResult, ct) =>
        await harness
          .RunRemote(
            parseResult.GetRequiredValue(server),
            parseResult.GetRequiredValue(project),
            parseResult.GetRequiredValue(model),
            parseResult.GetValue(version),
            parseResult.GetValue(destServerOption),
            parseResult.GetValue(destProjectOption),
            parseResult.GetValue(destModelOption),
            parseResult.GetValue(legacyApiOption),
            parseResult.GetValue(outOption),
            ct
          )
          .ConfigureAwait(false)
    );
    return cmd;
  }

  // Bundle output directory. Emptied on startup either way; the produced files are kept when it is given
  // explicitly, and deleted along with the temp dir when it is not.
  private static Option<string> OutOption()
  {
    Option<string> option = new("--out") { Description = "Output directory (default: a temp dir)." };
    option.Aliases.Add("--outputPath");
    return option;
  }

  private static Option<string[]> UploadOption() =>
    new("--upload")
    {
      Description = "Upload the bundle to <serverUrl> <projectId> <modelId> (token: SPECKLE_DST_TOKEN).",
      Arity = new ArgumentArity(3, 3),
      AllowMultipleArgumentsPerToken = true,
    };

  private static FileInfo ParseExistingFile(ArgumentResult result)
  {
    var file = new FileInfo(result.Tokens[0].Value);
    if (!file.Exists)
    {
      result.AddError($"File not found: {file.FullName}");
    }
    return file;
  }

  private static Uri ParseAbsoluteUri(ArgumentResult result)
  {
    if (Uri.TryCreate(result.Tokens[0].Value, UriKind.Absolute, out var uri))
    {
      return uri;
    }
    result.AddError($"Invalid absolute URL: {result.Tokens[0].Value}");
    return null!;
  }
}
