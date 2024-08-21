using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

public static class ClosureParser
{
  public static IEnumerable<string> GetChildrenIds(string rootObjectJson)
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
            return closureList.Select(x => x.Item1);
          }
          default:
            reader.Read();
            reader.Skip();
            break;
        }
      }
    }
    catch (Exception ex) when (!ex.IsFatal()) { }
    return [];
  }

  private static IEnumerable<(string, int)>? ReadObject(JsonTextReader reader)
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
              var closureList = ReadClosureEnumerable(reader);
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

  public static IReadOnlyList<(string, int)> GetClosures(JsonReader reader)
  {
    if (reader.TokenType != JsonToken.StartObject)
    {
      return Array.Empty<(string, int)>();
    }
    var closureList = ReadClosureEnumerable(reader).ToList();
    closureList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
    return closureList;
  }

  private static IEnumerable<(string, int)> ReadClosureEnumerable(JsonReader reader)
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
