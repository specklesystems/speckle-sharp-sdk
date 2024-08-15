using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

internal static class CallSiteCache
{
  // Adapted from the answer to
  // https://stackoverflow.com/questions/12057516/c-sharp-dynamicobject-dynamic-properties
  // by jbtule, https://stackoverflow.com/users/637783/jbtule
  // And also
  // https://github.com/mgravell/fast-member/blob/master/FastMember/CallSiteCache.cs
  // by Marc Gravell, https://github.com/mgravell
  private static readonly ConcurrentDictionary<string, CallSite<Func<CallSite, object, object?, object>>> s_setters =
    new();

  public static void SetValue(string propertyName, object target, object? value)
  {
    var site = s_setters.GetOrAdd(
      propertyName,
      name =>
      {
        var binder = Binder.SetMember(
          CSharpBinderFlags.None,
          name,
          typeof(CallSiteCache),
          new List<CSharpArgumentInfo>
          {
            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
            CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
          }
        );
        return CallSite<Func<CallSite, object, object?, object>>.Create(binder);
      }
    );
    site.Target.Invoke(site, target, value);
  }
}
