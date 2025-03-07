using FluentAssertions;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Tests.Unit.Host;

public class HostApplicationTests
{
  public static TheoryData<HostAppVersion> HostAppVersionData => new(Enum.GetValues<HostAppVersion>().ToList());

  [Theory]
  [MemberData(nameof(HostAppVersionData))]
  public void HostAppVersionParsingTests(HostAppVersion appVersion)
  {
    // Assert that the string representation starts with 'v'
    appVersion.ToString().StartsWith('v').Should().BeTrue();

    // Assert that the parsed version is a positive integer
    var version = HostApplications.GetVersion(appVersion);
    int.Parse(version).Should().BePositive();
  }
}
