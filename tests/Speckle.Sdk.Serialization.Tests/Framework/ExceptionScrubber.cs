using System.Text.RegularExpressions;
using Argon;

namespace Speckle.Sdk.Serialization.Tests.Framework;

public partial class ExceptionScrubber : WriteOnlyJsonConverter<Exception>
{
  //regex for matching < then GUID then >
  [GeneratedRegex(
    @"\<[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\>",
    RegexOptions.IgnoreCase,
    "en-US"
  )]
  private static partial Regex InternalizeStacktrace();

  [GeneratedRegex(" in(.*?)\\r\\n")]
  private static partial Regex RemoveSourceFiles();

  public override void Write(VerifyJsonWriter writer, Exception value)
  {
    if (value.StackTrace != null)
    {
      var x = InternalizeStacktrace().Replace(value.StackTrace, "<INTERNALIZED STACKTRACE>");
     x = RemoveSourceFiles().Replace(x, "\r\n");
      var ex = new JObject
      {
        ["Message"] = value.Message,
        ["Source"] = value.Source?.Trim(),
        ["StackTrace"] = x.Trim(),
      };
      writer.WriteRawValue(ex.ToString(Formatting.Indented));
      return;
    }
    base.Write(writer, value.ToString());
  }
}

public static class VerifyExtensions
{
  public static SettingsTask ScrubInteralizedStacktrace(this SettingsTask value)
  {
    value.AddExtraSettings(x => x.Converters.Add(new ExceptionScrubber()));
    return value;
  }
}
