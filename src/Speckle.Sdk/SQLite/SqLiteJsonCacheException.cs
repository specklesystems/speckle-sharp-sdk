using Microsoft.Data.Sqlite;

namespace Speckle.Sdk.SQLite;

public class SqLiteJsonCacheException : SpeckleException
{
  public SqLiteJsonCacheException() { }

  public SqLiteJsonCacheException(string message)
    : base(message) { }

  public SqLiteJsonCacheException(string message, Exception inner)
    : base(message, inner) { }

  public static SqLiteJsonCacheException Create(SqliteException inner)
  {
    if (!SqliteExceptions.SqliteErrorCodes.TryGetValue(inner.SqliteErrorCode, out string? errorMessage))
    {
      errorMessage = $"An error occurred while executing a SQLite command: {inner.SqliteErrorCode}";
    }
    if (!SqliteExceptions.SqliteErrorCodes.TryGetValue(inner.SqliteExtendedErrorCode, out string? detailedMessage))
    {
      detailedMessage = $"Detail: {inner.SqliteExtendedErrorCode}";
    }
    return new SqLiteJsonCacheException(
      $"An error occured with the SQLite cache:{Environment.NewLine}{errorMessage}{Environment.NewLine}{detailedMessage}",
      inner
    );
  }
}
