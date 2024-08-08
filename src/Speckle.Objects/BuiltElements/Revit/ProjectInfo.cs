using Speckle.Objects.Organization;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.Revit;

[SpeckleType("Objects.BuiltElements.Revit.ProjectInfo")]
public class ProjectInfo : BIMModelInfo
{
  public string author { get; set; }
  public string issueDate { get; set; }
  public string organizationDescription { get; set; }
  public string organizationName { get; set; }
}
