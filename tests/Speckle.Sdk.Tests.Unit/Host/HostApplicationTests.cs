using FluentAssertions;
using Shouldly;
using Speckle.Sdk.Host;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Host;

public class HostApplicationTests
{
  private static List<HostAppVersion> s_hostAppVersions = Enum.GetValues<HostAppVersion>().ToList();

  public static IEnumerable<object[]> HostAppVersionData =>
    s_hostAppVersions.Select(version => new object[] { version });

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
