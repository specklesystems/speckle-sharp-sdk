using Argon;

namespace Speckle.Sdk.Testing.Framework;

public class ExceptionScrubber : WriteOnlyJsonConverter<Exception>
{
  public ExceptionScrubber() { }

  public override void Write(VerifyJsonWriter writer, Exception value)
  {
    if (value.StackTrace != null)
    {
      var ex = new JObject
      {
        ["Type"] = value.GetType().FullName,
        ["Message"] = value.Message,
        ["Source"] = value.Source?.Trim(),
      };
      writer.WriteRawValue(ex.ToString(Formatting.Indented));
      return;
    }
    base.Write(writer, value.ToString());
  }
}
