using Speckle.Core.Kits;
using Speckle.Objects.BuiltElements.Revit;

namespace Speckle.Core.Serialization.Tests;

public class TestKit : ISpeckleKit
{
  public IEnumerable<Type> Types => typeof(RevitWall).Assembly.ExportedTypes;
  public IEnumerable<string> Converters { get; }
  public string Description { get; }
  public string Name { get; }
  public string Author { get; }
  public string WebsiteOrEmail { get; }

  public ISpeckleConverter LoadConverter(string app) => throw new NotImplementedException();
}
