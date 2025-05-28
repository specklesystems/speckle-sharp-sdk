using System.ComponentModel.DataAnnotations;
using Speckle.Sdk.Models;

namespace Speckle.Automate.Sdk.Integration;

public struct TestFunctionInputs
{
  [Required]
  public string ForbiddenSpeckleType { get; set; }
}

public static class TestAutomateFunction
{
  public static async Task Run(IAutomationContext automateContext, TestFunctionInputs testFunctionInputs)
  {
    Base versionRootObject = await automateContext.ReceiveVersion();

    int count = 0;
    if (versionRootObject.speckle_type == testFunctionInputs.ForbiddenSpeckleType)
    {
      if (versionRootObject.id is null)
      {
        throw new InvalidOperationException("Cannot operate on objects without their ids");
      }

      automateContext.AttachErrorToObjects(
        "",
        new[] { versionRootObject },
        $"This project should not contain the type: {testFunctionInputs.ForbiddenSpeckleType} "
      );
      count += 1;
    }

    if (count > 0)
    {
      automateContext.MarkRunFailed(
        "Automation failed: "
          + $"Found {count} object that have a forbidden speckle type: {testFunctionInputs.ForbiddenSpeckleType}"
      );
    }
    else
    {
      automateContext.MarkRunSuccess("No forbidden types found.");
    }
  }
}
