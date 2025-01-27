using System.Text.RegularExpressions;
using Argon;

namespace Speckle.Sdk.Serialization.Tests;

public partial class ExceptionScrubber : WriteOnlyJsonConverter<Exception>
{
  //regex for matching .< then GUID then >
  [GeneratedRegex(@"\.\<[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\>", RegexOptions.IgnoreCase, "en-US")]
  private static partial Regex InteralizeStacktrace();
  public override void Write(VerifyJsonWriter writer, Exception value)
  {
    if (value.StackTrace != null)
    {
      var ex = JsonConvert.SerializeObject(value, Formatting.Indented);
      var newString = InteralizeStacktrace().Replace(ex, ".<INTERNALIZED STACKTRACE>");
      writer.WriteRawValue(newString);
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
