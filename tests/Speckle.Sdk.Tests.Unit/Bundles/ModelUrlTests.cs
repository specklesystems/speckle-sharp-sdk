using Speckle.Sdk.Bundles;

namespace Speckle.Sdk.Tests.Unit.Bundles;

public class ModelUrlTests
{
  [Theory]
  [InlineData("https://app.speckle.systems/projects/9d39b8aa2f/models/622aea7379", "9d39b8aa2f", "622aea7379", null)]
  [InlineData(
    "https://next.speckle.dev/projects/9d39b8aa2f/models/622aea7379@08de6a66ec",
    "9d39b8aa2f",
    "622aea7379",
    "08de6a66ec"
  )]
  [InlineData("http://localhost:3000/projects/p/models/m@v/", "p", "m", "v")]
  public void Parse_ModelUrls(string url, string project, string model, string? version)
  {
    var parsed = ModelUrl.Parse(url);
    Assert.Equal(project, parsed.ProjectId);
    Assert.Equal(model, parsed.ModelId);
    Assert.Equal(version, parsed.VersionId);
    Assert.Equal(version is not null, parsed.HasVersion);
    Assert.Equal(new Uri(url).GetLeftPart(UriPartial.Authority), parsed.Server.ToString().TrimEnd('/'));
  }

  [Theory]
  [InlineData("")]
  [InlineData("not a url")]
  [InlineData("https://app.speckle.systems/projects/p")]
  [InlineData("https://app.speckle.systems/models/m/projects/p")]
  [InlineData("https://app.speckle.systems/projects/p/models/a,b")]
  [InlineData("https://app.speckle.systems/projects/p/models/m@")]
  public void Parse_Rejects(string url)
  {
    Assert.False(ModelUrl.TryParse(url, out _));
    Assert.Throws<ArgumentException>(() => ModelUrl.Parse(url));
  }

  [Fact]
  public void ToString_RoundTrips()
  {
    const string url = "https://next.speckle.dev/projects/p/models/m@v";
    Assert.Equal(url, ModelUrl.Parse(url).ToString());
  }
}
