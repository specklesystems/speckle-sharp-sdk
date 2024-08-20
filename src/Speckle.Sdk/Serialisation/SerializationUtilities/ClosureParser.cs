using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

public static class ClosureParser
{
  public static IReadOnlyList<(string, int)> GetClosures(JsonReader reader)
  {
    if (reader.TokenType != JsonToken.StartObject)
    {
      return Array.Empty<(string, int)>();
    }
    var closureList = ReadClosureList(reader);
    closureList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
    return closureList;
  }

  private static List<(string, int)> ReadClosureList(JsonReader reader)
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
