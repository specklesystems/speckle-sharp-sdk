using System.Text.RegularExpressions;

namespace Speckle.Sdk.Helpers;

public static class Constants
{
  public const double EPS = 1e-5;
  public const double SMALL_EPS = 1e-8;
  public const double EPS_SQUARED = EPS * EPS;

  public static readonly Regex ChunkPropertyNameRegex = new(@"^@\((\d*)\)"); //TODO: Experiment with compiled flag
}
