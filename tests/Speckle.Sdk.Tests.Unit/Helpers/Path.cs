using System.Runtime.InteropServices;
using FluentAssertions;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Tests.Unit.Helpers;

public class SpecklePathTests
{
  [Fact]
  public void TestUserApplicationDataPath()
  {
    var userPath = SpecklePathProvider.UserApplicationDataPath();
    string pattern;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      pattern = @"C:\\Users\\.*\\AppData\\Roaming";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      pattern = @"\/Users\/.*\/Library\/Application Support";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      // if running under root user, the .config folder is in another location...
      if (userPath.StartsWith("/root"))
      {
        pattern = @"\/root/\.config";
      }
      else
      {
        pattern = @"\/home/.*/\.config";
      }
    }
    else
    {
      throw new NotImplementedException("Your OS platform is not supported");
    }

    userPath.Should().MatchRegex(pattern);
  }

  [Fact]
  public void TestInstallApplicationDataPath()
  {
    var installPath = SpecklePathProvider.InstallApplicationDataPath;
    string pattern;

    if (string.IsNullOrEmpty(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)))
    {
      pattern = @"\/root";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      // this will prob fail on windows
      pattern = @"C:\\Users\\.*\\AppData\\Roaming";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      pattern = @"\/Users\/.*\/Library\/Application Support";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      // if running under root user, the .config folder is in another location...
      if (installPath.StartsWith("/root"))
      {
        pattern = @"\/root/\.config";
      }
      else
      {
        pattern = @"\/home/.*/\.config";
      }
    }
    else
    {
      throw new NotImplementedException("Your OS platform is not supported");
    }

    installPath.Should().MatchRegex(pattern);
  }
}
