using System.Runtime.CompilerServices;

namespace Speckle.Sdk;

public static class ScrutorFunctions
{
  public static bool IsNonAbstractClass(this Type type)
  {
    if (type.IsSpecialName)
    {
      return false;
    }

    if (type.IsClass && !type.IsAbstract)
    {
      if (type.HasAttribute<CompilerGeneratedAttribute>())
      {
        return false;
      }

      return type.IsPublic || type.IsNestedPublic;
    }

    return false;
  }

  public static bool HasAttribute<T>(this Type type)
    where T : Attribute
  {
    return type.HasAttribute(typeof(T));
  }

  public static bool HasAttribute(this Type type, Type attributeType)
  {
    return type.IsDefined(attributeType, inherit: true);
  }

  public static IEnumerable<Type> FindMatchingInterface(this Type type)
  {
    var matchingInterfaceName = $"I{type.Name}";

    var matchedInterfaces = GetImplementedInterfacesToMap(type)
      .Where(x => string.Equals(x.Name, matchingInterfaceName, StringComparison.Ordinal))
      .ToArray();

    if (matchedInterfaces.Length == 0)
    {
      yield break;
    }

    Type? matchingType = matchedInterfaces.FirstOrDefault();

    if (matchingType is null)
    {
      yield break;
    }

    yield return matchingType;
  }

  private static IEnumerable<Type> GetImplementedInterfacesToMap(Type type)
  {
    if (!type.IsGenericType)
    {
      return type.GetInterfaces();
    }

    if (!type.IsGenericTypeDefinition)
    {
      return type.GetInterfaces();
    }

    return FilterMatchingGenericInterfaces(type);
  }

  private static IEnumerable<Type> FilterMatchingGenericInterfaces(Type type)
  {
    var genericArguments = type.GetGenericArguments();

    foreach (var current in type.GetInterfaces())
    {
      if (
        current.IsGenericType
        && current.ContainsGenericParameters
        && GenericParametersMatch(genericArguments, current.GetGenericArguments())
      )
      {
        yield return current.GetGenericTypeDefinition();
      }
    }
  }

  private static bool GenericParametersMatch(IReadOnlyList<Type> parameters, IReadOnlyList<Type> interfaceArguments)
  {
    if (parameters.Count != interfaceArguments.Count)
    {
      return false;
    }

    for (var i = 0; i < parameters.Count; i++)
    {
      if (parameters[i] != interfaceArguments[i])
      {
        return false;
      }
    }

    return true;
  }
}
