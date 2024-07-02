using Speckle.Core.Models;

namespace Speckle.Core.SchemaVersioning;

public class TestBaseA : Base
{
  
}

public class TestBaseB : Base
{
  
}

public interface IObjectSchemaUpgrade<in TInputType, out TOutputType> where TInputType : class where TOutputType : class
{
  TOutputType Upgrade(TInputType incomingVersion);
}

public class WallObjectUpgrader : IObjectSchemaUpgrade<Base, Base>
{
  public TestBaseB Upgrade(TestBaseA input)
  {
    return new TestBaseB();
  }

  public Base Upgrade(Base incomingVersion) => Upgrade((TestBaseA) incomingVersion);
}

public interface ISchemaObjectUpgrader<in TInputType, out TOutputType>
  where TInputType : class where TOutputType : class
{
  
}


public class SchemaObjectUpgrader<TInputType, TOutputType> : ISchemaObjectUpgrader<TInputType, TOutputType>
  where TInputType : class where TOutputType : class
{
  // look for stuff in assemblies like the type cache, this is somewhat similar
  void BuildUpgrader()
  {
    // do we look for ISchemaObjectUpgrader<TInputType, TOutputType>
    // do we look for a from and to value on some attribute?
    // do have some abstract base that validates the input type matches, i.e. that the input that was as a Base
    // matches the output
    // should we consider linking this machinery to the typecache???
  }

  // question - could we just add typed/naming convention methods to the various classes?
}
