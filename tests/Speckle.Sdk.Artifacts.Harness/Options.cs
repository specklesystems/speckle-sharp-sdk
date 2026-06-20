namespace Speckle.Sdk.Artifacts.Harness;

public enum InputMode
{
  Local,
  Remote
}

/// <summary>Parsed CLI options for the harness. See <see cref="Usage"/>.</summary>
public sealed class Options
{
  public InputMode Mode { get; private set; } = InputMode.Local;

  // local
  public string? LocalPath { get; private set; }
  public string LocalRoot { get; private set; } = "auto";

  // remote source
  public string? SrcServerUrl { get; private set; }
  public string? SrcProjectId { get; private set; }
  public string? SrcModelId { get; private set; }
  public string? SrcVersionId { get; private set; }

  // output
  public string? OutDir { get; private set; }

  // upload destination
  public bool Upload { get; private set; }
  public string? DstServerUrl { get; private set; }
  public string? DstProjectId { get; private set; }
  public string? DstModelId { get; private set; }

  public const string Usage =
    """
    usage:
      INPUT (one of):
        --local <ndjsonPath> [--root <id|auto>]
        --remote <serverUrl> <projectId> <modelId> [--version <versionId>]   (env: SPECKLE_SRC_TOKEN)
      OUTPUT:
        --out <dir>                                  (default: a temp dir)
        --upload <serverUrl> <projectId> <modelId>   (env: SPECKLE_DST_TOKEN)
      Both --out and --upload may apply. --upload implies a temp dir if --out is absent.

      Legacy positional form is still accepted:
        <ndjsonPath> [rootId|auto] [outDir]
    """;

  public static Options Parse(string[] args)
  {
    var o = new Options();

    // Legacy positional fallback: first token is not a recognised flag.
    if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
    {
      o.Mode = InputMode.Local;
      o.LocalPath = args[0];
      if (args.Length > 1)
      {
        o.LocalRoot = args[1];
      }
      if (args.Length > 2)
      {
        o.OutDir = args[2];
      }
      o.Validate();
      return o;
    }

    var inputSet = false;
    for (var i = 0; i < args.Length; i++)
    {
      switch (args[i])
      {
        case "--local":
          o.Mode = InputMode.Local;
          o.LocalPath = Next(args, ref i, "--local");
          inputSet = true;
          break;
        case "--root":
          o.LocalRoot = Next(args, ref i, "--root");
          break;
        case "--remote":
          o.Mode = InputMode.Remote;
          o.SrcServerUrl = Next(args, ref i, "--remote serverUrl");
          o.SrcProjectId = Next(args, ref i, "--remote projectId");
          o.SrcModelId = Next(args, ref i, "--remote modelId");
          inputSet = true;
          break;
        case "--version":
          o.SrcVersionId = Next(args, ref i, "--version");
          break;
        case "--out":
          o.OutDir = Next(args, ref i, "--out");
          break;
        case "--upload":
          o.Upload = true;
          o.DstServerUrl = Next(args, ref i, "--upload serverUrl");
          o.DstProjectId = Next(args, ref i, "--upload projectId");
          o.DstModelId = Next(args, ref i, "--upload modelId");
          break;
        default:
          throw new ArgumentException($"unknown argument '{args[i]}'");
      }
    }

    if (!inputSet)
    {
      throw new ArgumentException("no input specified (use --local or --remote)");
    }

    o.Validate();
    return o;
  }

  private void Validate()
  {
    if (Mode == InputMode.Local)
    {
      if (string.IsNullOrWhiteSpace(LocalPath))
      {
        throw new ArgumentException("--local requires a path");
      }
    }
    else
    {
      if (
        string.IsNullOrWhiteSpace(SrcServerUrl)
        || string.IsNullOrWhiteSpace(SrcProjectId)
        || string.IsNullOrWhiteSpace(SrcModelId)
      )
      {
        throw new ArgumentException("--remote requires <serverUrl> <projectId> <modelId>");
      }
    }

    if (
      Upload
      && (
        string.IsNullOrWhiteSpace(DstServerUrl)
        || string.IsNullOrWhiteSpace(DstProjectId)
        || string.IsNullOrWhiteSpace(DstModelId)
      )
    )
    {
      throw new ArgumentException("--upload requires <serverUrl> <projectId> <modelId>");
    }
  }

  private static string Next(string[] args, ref int i, string what)
  {
    if (i + 1 >= args.Length)
    {
      throw new ArgumentException($"missing value for {what}");
    }
    return args[++i];
  }
}
