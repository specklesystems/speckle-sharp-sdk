using BCLBase64Url = System.Buffers.Text.Base64Url;

namespace Speckle.Sdk.Dependencies;

/// <summary>
/// Pollyfills for <see cref="System.Buffers.Text.Base64Url"/>
/// to bring .NET 9 features to lower targets
/// </summary>
public static class Base64Url
{
  public static string EncodeToString(ReadOnlySpan<byte> source) => BCLBase64Url.EncodeToString(source);
}
