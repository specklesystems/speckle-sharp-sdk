using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.SQLite;

[GenerateAutoInterface]
public class SqLiteJsonCacheManagerFactory : ISqLiteJsonCacheManagerFactory
{
  public const int INITIAL_CONCURRENCY = 4;

  private ISqLiteJsonCacheManager Create(string path, int concurrency) =>  SqLiteJsonCacheManager.FromFilePath(path, concurrency);

  public ISqLiteJsonCacheManager CreateForUser(string scope) =>
    Create(Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db"), 1);

  public ISqLiteJsonCacheManager CreateFromStream(string streamId) =>
    Create(SqlitePaths.GetDBPath(streamId), INITIAL_CONCURRENCY);
}
