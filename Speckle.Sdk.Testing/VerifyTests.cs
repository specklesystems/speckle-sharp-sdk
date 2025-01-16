namespace Speckle.Sdk.Testing;

public class VerifyTests
{
  [Fact]
  public Task TestVerify() => VerifyChecks.Run();
}
