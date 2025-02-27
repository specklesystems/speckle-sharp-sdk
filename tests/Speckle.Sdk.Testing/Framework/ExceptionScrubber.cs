using Argon;

namespace Speckle.Sdk.Testing.Framework;

public class ExceptionScrubber : WriteOnlyJsonConverter<Exception>
{
  public ExceptionScrubber() { }

  public override void Write(VerifyJsonWriter writer, Exception value)
  {
    var ex = new JObject
    {
      ["Type"] = value.GetType().FullName,
      ["Message"] = value.Message,
      ["Source"] = value.Source?.Trim(),
    };
    if (value.StackTrace != null)
    {
      ex["StackTrace"] = value.StackTrace;
    }
    writer.WriteRawValue(ex.ToString(Formatting.Indented));
  }
}
