using Speckle.Sdk.Common;

namespace Speckle.Sdk.Testing.Framework;

public class AggregationExceptionScrubber : WriteOnlyJsonConverter<AggregateException>
{
  private static readonly ExceptionScrubber _innerScrubber = new();
  public override void Write(VerifyJsonWriter writer, AggregateException exception)
  {
    writer.WriteStartObject();

    writer.WriteMember(exception, exception.GetType().FullName, "Type");
    if (exception.InnerExceptions.Count == 1)
    {
      writer.WritePropertyName("InnerException");
      _innerScrubber.Write(writer, exception.InnerException.NotNull());
    }
    else
    {
      writer.WritePropertyName("InnerExceptions");
      writer.WriteStartArray();
      foreach (var innerException in exception.InnerExceptions)
      {
        _innerScrubber.Write(writer, innerException);
        
      }
      writer.WriteEndArray();
    }

    writer.WriteEndObject();
  }
}
