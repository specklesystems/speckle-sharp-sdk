using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Properties;

public class CSIDiaphragm : Base
{
  [SchemaInfo("CSI Diaphragm", "Create an CSI Diaphragm", "CSI", "Properties")]
  public CSIDiaphragm(string name, bool semiRigid)
  {
    this.name = name;
    SemiRigid = semiRigid;
  }

  public CSIDiaphragm() { }

  public string name { get; set; }
  public bool SemiRigid { get; set; }
}
