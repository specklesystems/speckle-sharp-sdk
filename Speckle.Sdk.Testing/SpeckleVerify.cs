using System.Runtime.CompilerServices;
using Argon;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Testing.Framework;

namespace Speckle.Sdk.Testing;

public static class SpeckleVerify
{
  private static bool _initialized;

  [ModuleInitializer]
  public static void Initialize()
  {
    if (_initialized)
    {
      return;
    }

    _initialized = true;
    VerifierSettings.DontScrubGuids();
    VerifierSettings.DontScrubDateTimes();

    VerifierSettings.UseStrictJson();
    VerifierSettings.DontIgnoreEmptyCollections();
    VerifierSettings.SortPropertiesAlphabetically();
    VerifierSettings.SortJsonObjects();
    VerifierSettings.AddExtraSettings(x =>
    {
      var existing = x.Converters.OfType<WriteOnlyJsonConverter<AggregateException>>().First();
      x.Converters.Remove(existing);
      x.Converters.Add(new AggregationExceptionScrubber());
      x.Converters.Add(new ExceptionScrubber());
    });
    VerifyQuibble.Initialize();
  }

  private static readonly JsonSerializer _jsonSerializer = new()
  {
    NullValueHandling = NullValueHandling.Include,
    Formatting = Formatting.Indented,
    Converters = { new JsonStringSerializer() },
  };

  private static SettingsTask VerifyJsonObjects(IDictionary<string, Json> objects, string sourceFile) =>
    VerifyJson(JObject.FromObject(objects, _jsonSerializer).ToString(), sourceFile: sourceFile);

  public static SettingsTask VerifyJsonDictionary(
    IDictionary<string, string> objects,
    [CallerFilePath] string sourceFile = ""
  ) => VerifyJsonObjects(objects.ToDictionary(x => x.Key, x => new Json(x.Value)), sourceFile);

  public static SettingsTask VerifyJsonDictionary(
    IDictionary<Id, Json> objects,
    [CallerFilePath] string sourceFile = ""
  ) => VerifyJsonObjects(objects.ToDictionary(x => x.Key.Value, x => x.Value), sourceFile);
}
