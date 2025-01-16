using System.Runtime.CompilerServices;
using Argon;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Testing.Framework;

namespace Speckle.Sdk.Testing;

public static class SpeckleVerify
{
    [ModuleInitializer]
    public static void Initialize()
    {
      VerifierSettings.DontScrubGuids();
      VerifierSettings.DontScrubDateTimes();

      VerifierSettings.UseStrictJson();
      VerifierSettings.DontIgnoreEmptyCollections();
      VerifierSettings.SortPropertiesAlphabetically();
      VerifierSettings.SortJsonObjects();
      if (!VerifyQuibble.Initialized)
      {
        VerifyQuibble.Initialize();
      }
    }
    
    
    private static readonly JsonSerializer _jsonSerializer = new()
    {
      NullValueHandling = NullValueHandling.Include,
      Formatting = Formatting.Indented,
      Converters = { new JsonStringSerializer() },
    };

    private static async Task VerifyJsonObjects(IDictionary<string, Json> objects, string sourceFile) =>
      await VerifyJson(JObject.FromObject(objects, _jsonSerializer).ToString(), sourceFile: sourceFile);

    public static async Task VerifyJsonDictionary(
      IDictionary<string, string> objects,
      [CallerFilePath] string sourceFile = ""
    ) => await VerifyJsonObjects(objects.ToDictionary(x => x.Key, x => new Json(x.Value)), sourceFile);

    public static async Task VerifyJsonDictionary(
      IDictionary<Id, Json> objects,
      [CallerFilePath] string sourceFile = ""
    ) => await VerifyJsonObjects(objects.ToDictionary(x => x.Key.Value, x => x.Value), sourceFile);
}
