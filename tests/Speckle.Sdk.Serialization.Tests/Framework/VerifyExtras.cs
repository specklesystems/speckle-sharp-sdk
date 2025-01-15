using System.Runtime.CompilerServices;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Serialization.Tests.Framework;

public static class VerifyExtras
{
  [ModuleInitializer]
  public static void Initialize()
  {
    VerifierSettings.DontScrubGuids();
    VerifierSettings.AddExtraSettings(x => x.Converters.Insert(0, new JsonStringSerializer()));
    VerifierSettings.AddExtraSettings(x => x.Converters.Insert(0, new IdStringSerializer()));
  }

  public static async Task VerifyJsonDictionary(
    IDictionary<string, string> objects,
    [CallerFilePath] string sourceFile = ""
  ) => await Verify(objects.Select(x => new JsonItem(x)), sourceFile: sourceFile);

  public static async Task VerifyJsonDictionary(
    IDictionary<Id, Json> objects,
    [CallerFilePath] string sourceFile = ""
  ) => await Verify(objects.Select(x => x), sourceFile: sourceFile);

  private readonly record struct JsonItem
  {
    private readonly KeyValuePair<string, string> _item;

    public JsonItem(KeyValuePair<string, string> item)
    {
      _item = item;
    }

    public string Id => _item.Key;
    public Json Json => new(_item.Value);
  }
}
