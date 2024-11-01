using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialization.Tests;

public class BaseComparer : IEqualityComparer<Base>
{
  public bool Equals(Base? x, Base? y)
  {
    if (ReferenceEquals(x, y))
      return true;
    if (x is null)
      return false;
    if (y is null)
      return false;
    Type type = x.GetType();
    if (type != y.GetType())
      return false;
    var types = DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic | DynamicBaseMemberType.SchemaIgnored;
    var membersX = x.GetMembers(types);
    var membersY = y.GetMembers(types);
    if (membersX.Count != membersY.Count)
      return false;
    foreach (var kvp in membersX)
    {
      var propertyInfo = type.GetProperty(kvp.Key);
      if (propertyInfo is not null && !propertyInfo.CanWrite)
      {
        continue;
      }
      if (y[kvp.Key] != kvp.Value)
        return false;
    }
    return x.id == y.id && x.applicationId == y.applicationId;
  }

  public int GetHashCode(Base obj)
  {
    return HashCode.Combine(obj.id, obj.applicationId);
  }
}
