using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.Utilities;

public static class ClosureParser
{
  public static IReadOnlyList<(string, int)> GetClosures(string rootObjectJson)
  {
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
            var closureList = ReadObject(reader);
            if (closureList is not null && closureList.Count != 0)
            {
              closureList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
              return closureList;
            }
            return Array.Empty<(string, int)>();
          }
          default:
            reader.Read();
            reader.Skip();
            break;
        }
      }
    }
    catch (Exception ex) when (!ex.IsFatal()) { }
    return Array.Empty<(string, int)>();
  }

  private static List<(string, int)>? ReadObject(JsonTextReader reader)
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
              reader.Read(); //goes to prop vale
              var closureList = ReadClosureList(reader);
              return closureList;
            }
            reader.Read(); //goes to prop vale
            reader.Skip();
            reader.Read(); //goes to next
          }
          break;
        default:
          reader.Read();
          reader.Skip();
          reader.Read();
          break;
      }
    }
    return null;
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
