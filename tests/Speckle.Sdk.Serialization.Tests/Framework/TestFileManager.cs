using System.IO.Compression;
using System.Reflection;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Data;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialization.Tests.Framework;

public static class TestFileManager
{
  static TestFileManager()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataObject).Assembly, _assembly);
  }

  private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();
  private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> _objects = new();

  public static async Task<IReadOnlyDictionary<string, string>> GetFileAsClosures(string fileName)
  {
    if (!_objects.TryGetValue(fileName, out var closure))
    {
      var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
      var json = await ReadJson(fullName);
      closure = ReadAsObjects(json);
      _objects.Add(fileName, closure);
    }
    return closure;
  }

  private static async Task<string> ReadJson(string fullName)
  {
    await using var stream = _assembly.GetManifestResourceStream(fullName).NotNull();
    if (fullName.EndsWith(".gz"))
    {
      await using var z = new GZipStream(stream, CompressionMode.Decompress);
      using var reader2 = new StreamReader(z);
      return await reader2.ReadToEndAsync();
    }
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
  }

  private static Dictionary<string, string> ReadAsObjects(string json)
  {
    var jsonObjects = new Dictionary<string, string>();
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
