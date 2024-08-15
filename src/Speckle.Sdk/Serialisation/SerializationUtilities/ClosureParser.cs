using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

public static class ClosureParser
{
  public static List<(string, int)> GetClosures(string rootObjectJson)
  {
    List<(string, int)>? closureList = null;
    try
    {
      using JsonTextReader reader = new(new StringReader(rootObjectJson));
      reader.Read();
      while (reader.TokenType != JsonToken.EndObject)
      {
        switch (reader.TokenType)
        {
          case JsonToken.StartObject:
          {
            closureList = ReadObject(reader);
            if (closureList.Any())
            {
              return closureList;
            }
            break;
          }
          default:
            reader.Read();
            reader.Skip();
            break;
        }
      }
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
    }
    return closureList ?? new List<(string, int)>(Array.Empty<(string, int)>());
  }
  
  
  private static List<(string, int)> ReadObject(JsonTextReader reader)
  {
    reader.Read();
    while (reader.TokenType != JsonToken.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
        {
          if (reader.Value as string == "__closure")
          {
            reader.Read();
            var closureList = ReadClosureList(reader);
            return closureList;
          }

          reader.Read();
          reader.Skip();
          reader.Read();
        }
          break;
        default:
          reader.Read();
          reader.Skip();
          reader.Read();
          break;
      }
    }
    return [..Array.Empty<(string, int)>()];
  }
  

 
  private static List<(string, int)> ReadClosureList(JsonTextReader reader)
  {
    List<(string, int)> closureList = new();
    reader.Read(); //startobject
    while (reader.TokenType != JsonToken.EndObject)
    {
      var childId = (reader.Value as string).NotNull(); // propertyName
      int childMinDepth = reader.ReadAsInt32().NotNull(); //propertyValue
      reader.Read();
      closureList.Add((childId, childMinDepth));
    }

    return closureList;
  }
}
