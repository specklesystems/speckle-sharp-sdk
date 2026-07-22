using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// Builds the harness command tree (System.CommandLine 2.0.0 GA). Each leaf action resolves the service
/// it needs from the DI container and calls a typed <see cref="Harness"/> entry point; the action's exit
/// code becomes the process exit code.
/// </summary>
internal static class HarnessCommandLine
{
  public static RootCommand Build(IServiceProvider services)
  {
    RootCommand root = new("Speckle artefact-bundle migration harness.");
    root.Subcommands.Add(BuildSelfTest(services));
    root.Subcommands.Add(BuildLocal(services));
    root.Subcommands.Add(BuildRemote(services));
    return root;
  }

  private static Command BuildSelfTest(IServiceProvider services)
  {
    Command cmd = new("selftest", "Run the SGEO encoder byte-layout self-test.");
    cmd.SetAction(_ => services.GetRequiredService<SgeoSelfTest>().Run());
    return cmd;
  }

  private static Command BuildLocal(IServiceProvider services)
  {
    Argument<FileInfo> ndjson = new("ndjsonPath")
    {
      Description = "Path to the NDJSON graph file (.ndjson, .gz, or .zip).",
      CustomParser = ParseExistingFile,
    };
    Option<string> rootOption = new("--root")
    {
      Description = "Root object id, or 'auto' to detect it.",
      DefaultValueFactory = _ => "auto",
    };
    Option<string> outOption = OutOption();
    Option<string[]> uploadOption = UploadOption();

    Command cmd = new("local", "Migrate a local NDJSON graph.");
    cmd.Arguments.Add(ndjson);
    cmd.Options.Add(rootOption);
    cmd.Options.Add(outOption);
    cmd.Options.Add(uploadOption);
    cmd.SetAction(
      async (parseResult, ct) =>
        await services
          .GetRequiredService<Harness>()
          .RunLocal(
            parseResult.GetValue(ndjson)!,
            parseResult.GetValue(rootOption)!,
            parseResult.GetValue(outOption),
            parseResult.GetValue(uploadOption),
            ct
          )
          .ConfigureAwait(false)
    );
    return cmd;
  }

  private static Command BuildRemote(IServiceProvider services)
  {
    Argument<Uri> server = new("serverUrl") { Description = "Source Speckle server URL.", CustomParser = ParseAbsoluteUri };
    Argument<string> project = new("projectId") { Description = "Source project id." };
    Argument<string> model = new("modelId") { Description = "Source model id." };
    Option<string> versionOption = new("--version") { Description = "Source version id (default: latest)." };
    Option<string> outOption = OutOption();
    Option<string[]> uploadOption = UploadOption();

    Command cmd = new("remote", "Migrate a graph from a remote Speckle server (token: SPECKLE_SRC_TOKEN).");
    cmd.Arguments.Add(server);
    cmd.Arguments.Add(project);
    cmd.Arguments.Add(model);
    cmd.Options.Add(versionOption);
    cmd.Options.Add(outOption);
    cmd.Options.Add(uploadOption);
    cmd.SetAction(
      async (parseResult, ct) =>
        await services
          .GetRequiredService<Harness>()
          .RunRemote(
            parseResult.GetValue(server)!,
            parseResult.GetValue(project)!,
            parseResult.GetValue(model)!,
            parseResult.GetValue(versionOption),
            parseResult.GetValue(outOption),
            parseResult.GetValue(uploadOption),
            ct
          )
          .ConfigureAwait(false)
    );
    return cmd;
  }

  private static Option<string> OutOption() => new("--out") { Description = "Output directory (default: a temp dir)." };

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
