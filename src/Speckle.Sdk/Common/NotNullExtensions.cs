using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Common;

public static class NotNullExtensions
{
  /// <exception cref="ArgumentNullException">Thrown when the awaited <paramref name="task"/> returns <see langword="null"/></exception>
  public static async ValueTask<T> NotNull<T>(
    this ValueTask<T?> task,
    [CallerArgumentExpression(nameof(task))] string? message = null
  )
    where T : class
  {
    var x = await task.ConfigureAwait(false);
    if (x is null)
    {
      throw new ArgumentNullException(message ?? "Value is null");
    }
    return x;
  }

  /// <inheritdoc cref="NotNull{T}(System.Threading.Tasks.ValueTask{T?},string?)"/>
  public static async ValueTask<T> NotNull<T>(
    this ValueTask<T?> task,
    [CallerArgumentExpression(nameof(task))] string? message = null
  )
    where T : struct
  {
    var x = await task.ConfigureAwait(false);
    if (x is null)
    {
      throw new ArgumentNullException(message ?? "Value is null");
    }
    return x.Value;
  }

  /// <inheritdoc cref="NotNull{T}(System.Threading.Tasks.ValueTask{T?},string?)"/>
  public static async Task<T> NotNull<T>(
    this Task<T?> task,
    [CallerArgumentExpression(nameof(task))] string? message = null
  )
    where T : class
  {
    var x = await task.ConfigureAwait(false);
    if (x is null)
    {
      throw new ArgumentNullException(message ?? "Value is null");
    }
    return x;
  }

  /// <inheritdoc cref="NotNull{T}(System.Threading.Tasks.ValueTask{T?},string?)"/>
  public static async Task<T> NotNull<T>(
    this Task<T?> task,
    [CallerArgumentExpression(nameof(task))] string? message = null
  )
    where T : struct
  {
    var x = await task.ConfigureAwait(false);
    if (x is null)
    {
      throw new ArgumentNullException(message ?? "Value is null");
    }
    return x.Value;
  }

  /// <exception cref="ArgumentNullException">Thrown when <paramref name="obj"/> is <see langword="null"/></exception>
  public static T NotNull<T>([NotNull] this T? obj, [CallerArgumentExpression(nameof(obj))] string? paramName = null)
    where T : class
  {
    if (obj is null)
    {
      throw new ArgumentNullException(paramName ?? "Value is null");
    }
    return obj;
  }

  /// <inheritdoc cref="NotNull{T}(T?,string?)"/>
  public static T NotNull<T>([NotNull] this T? obj, [CallerArgumentExpression(nameof(obj))] string? paramName = null)
    where T : struct
  {
    if (obj is null)
    {
      throw new ArgumentNullException(paramName ?? "Value is null");
    }
    return obj.Value;
  }

  public static IEnumerable<T> Empty<T>(this IEnumerable<T>? obj)
  {
    if (obj is null)
    {
      return Enumerable.Empty<T>();
    }
    return obj;
  }

  public static string NotNullOrWhiteSpace(
    [NotNull] this string? value,
    [CallerArgumentExpression(nameof(value))] string? paramName = null
  )
  {
    if (value is null)
    {
      throw new ArgumentNullException(paramName ?? "Value is null");
    }

    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
    }

    return value;
  }
}
