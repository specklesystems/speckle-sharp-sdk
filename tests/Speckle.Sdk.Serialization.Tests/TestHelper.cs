using System.IO.Compression;
using System.Reflection;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialization.Tests;

public static class TestHelper
{
  private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

  public static async Task<string> ReadJsonFromResource(string resourceName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(resourceName));
    return await ReadJson(fullName);
  }

  public static async Task<string> ReadJson(string fullName)
  {
    await using var stream = _assembly.GetManifestResourceStream(fullName).NotNull();
    if (fullName.EndsWith(".json.gz"))
    {
      await using var gZipStream = new GZipStream(stream, CompressionMode.Decompress);
      using var reader = new StreamReader(gZipStream);
      return await reader.ReadToEndAsync();
    }
    else
    {
      using var reader = new StreamReader(stream);
      return await reader.ReadToEndAsync();
    }
  }

  public static async Task<Dictionary<string, string>> ReadAsObjectsFromResource(string resourceName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(resourceName));
    return await ReadAsObjects(fullName);
  }

  public static async Task<Dictionary<string, string>> ReadAsObjects(string fullName)
  {
    var jsonObjects = new Dictionary<string, string>();
    var json = await ReadJson(fullName);
    var array = JArray.Parse(json);
    foreach (var obj in array)
    {
      if (obj is JObject jobj)
      {
        jsonObjects.Add(jobj["id"].NotNull().Value<string>().NotNull(), jobj.ToString());
      }
    }
    return jsonObjects;
  }
}
