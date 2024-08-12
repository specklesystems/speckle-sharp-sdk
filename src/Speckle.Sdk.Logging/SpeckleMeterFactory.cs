using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public static class SpeckleMeterFactory
{
  private static readonly Meter s_meter = new(Consts.Application, Consts.Version);

  
  public static ISpeckleCounter<T> GetCounter<T>(string name, string? unit = null)
  where T : struct
  {
    var counter = s_meter.CreateCounter<T>(name, unit);
    return new SpeckleCounter<T>(counter);
  }
  
  public static ISpeckleUpDownCounter<T> CreateUpDownCounter<T>(string? name = null, [CallerMemberName] string source = "", string? unit = null, params KeyValuePair<string, object?>[] tags)
    where T : struct
  {
    var counter = s_meter.CreateUpDownCounter<T>(name ?? source, unit, null, tags);
    return new SpeckleUpDownCounter<T>(counter);
  }
  public static ISpeckleHistogram<T> CreateHistogram<T>(string? name = null, [CallerMemberName] string source = "", string? unit = null,  params KeyValuePair<string, object?>[] tags)
    where T : struct
  {
    var counter = s_meter.CreateHistogram<T>(name ?? source, unit, null, tags);
    return new SpeckleHistogram<T>(counter);
  }
}
