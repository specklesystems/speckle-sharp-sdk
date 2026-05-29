using System.Text.Json;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive;

public class Foo
{
  private readonly JsonSerializerOptions _options;

  public Foo()
  {
    _options = new JsonSerializerOptions();
    _options.Converters.Add(new SpeckleJsonConverter(new()));
  }

  [Fact]
  public void Fooo()
  {
    //language=json
    string raw = """
      {
        "speckle_type" : "Speckle.Core.Models.Collections.Collection",
        "name": "this is my collection",
        "id": "abcdef",
        "dynamic_prop": false,
        "elements": [
            {
            "speckle_type" : "Speckle.Core.Models.Collections.Collection",
            "name": "this is my inner collection",
            "id": "bxcbvvbcx"
            }
          ]
      }
      """;

    Base? result = JsonSerializer.Deserialize<Base>(raw, _options);
    Console.WriteLine(result?.id);
  }
}
