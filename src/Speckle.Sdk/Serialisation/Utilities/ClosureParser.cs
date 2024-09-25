using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation.Send;

namespace Speckle.Sdk.Serialisation.Utilities;

public static class ClosureParser
{
  public static IReadOnlyList<(string id, int depth)> GetClosures(string rootObjectJson)
  {
    try
    {
      using JsonTextReader reader = SpeckleObjectSerializer2Pool.Instance.GetJsonTextReader(
        new StringReader(rootObjectJson)
      );
      reader.Read();
      while (reader.TokenType != JsonToken.EndObject)
      {
        switch (reader.TokenType)
        {
          case JsonToken.StartObject:
          {
            var closureList = ReadObject(reader);
            return closureList;
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

  public static IEnumerable<string> GetChildrenIds(string rootObjectJson) =>
    GetClosures(rootObjectJson).Select(x => x.id);

  private static IReadOnlyList<(string, int)> ReadObject(JsonTextReader reader)
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
    return [];
  }

  public static IReadOnlyList<(string, int)> GetClosures(JsonReader reader)
  {
    if (reader.TokenType != JsonToken.StartObject)
    {
      return Array.Empty<(string, int)>();
    }

    var closureList = ReadClosureEnumerable(reader);
    closureList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
    return closureList;
  }

  private static List<(string, int)> ReadClosureEnumerable(JsonReader reader)
  {
    List<(string, int)> closureList = new();
    reader.Read(); //startobject
    while (reader.TokenType != JsonToken.EndObject)
    {
      var childId = (reader.Value as string).NotNull(); // propertyName
      int childMinDepth = (reader.ReadAsInt32()).NotNull(); //propertyValue
      reader.Read();
      closureList.Add((childId, childMinDepth));
    }
    return closureList;
  }
}
