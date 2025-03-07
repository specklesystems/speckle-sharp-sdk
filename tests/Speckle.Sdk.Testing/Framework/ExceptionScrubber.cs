using Argon;

namespace Speckle.Sdk.Testing.Framework;

public class ExceptionScrubber : WriteOnlyJsonConverter<Exception>
{
  public override void Write(VerifyJsonWriter writer, Exception value)
  {
    var ex = new JObject
    {
      ["Type"] = value.GetType().FullName,
      ["Message"] = value.Message,
      ["Source"] = value.Source?.Trim(),
    };
    //intentionally removed stacktrace to avoid errors on different machines and line numbers
    writer.WriteRawValue(ex.ToString(Formatting.Indented));
  }
}
