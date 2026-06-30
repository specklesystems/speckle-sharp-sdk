using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Unit.Credentials;

public class AuthFlowTests
{
  private const int REPEAT = 20;

  [Fact]
  public void GenerateChallenge_ReturnsValidUniqueChallenge()
  {
    var codeVerifiers = Enumerable.Range(0, REPEAT).Select(_ => AuthFlow.GenerateCodeVerifier()).ToArray();

    Assert.All(
      codeVerifiers,
      item =>
      {
        Assert.Equal(43, item.Length);
        Assert.Matches(@"^[A-Za-z0-9\-_+/]*$", item);
      }
    );

    Assert.Equivalent(codeVerifiers, codeVerifiers.Distinct());
    var challenges = codeVerifiers.Select(AuthFlow.GenerateCodeChallenge).ToArray();

    Assert.All(
      challenges,
      item =>
      {
        Assert.Equal(43, item.Length);
        Assert.Matches(@"^[A-Za-z0-9\-_+/]*$", item);
      }
    );
    Assert.Equivalent(challenges, challenges.Distinct());
  }
}
