using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Tests.Unit.Credentials;

public class AuthFlowTests
{
  [Fact]
  public void GenerateChallenge_ReturnsValidChallenge()
  {
    var challenge = AuthFlow.GenerateChallenge();

    Assert.Equal(43, challenge.Length);
    Assert.Matches(@"^[A-Za-z0-9\-_+/]*$", challenge);
  }
}
