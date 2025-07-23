using Speckle.InterfaceGenerator;
using Speckle.Sdk.Caching;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.SQLite;

[GenerateAutoInterface]
public class SqLiteJsonCacheManagerFactory(IModelCacheManager modelCacheManager) : ISqLiteJsonCacheManagerFactory
{
  public const int INITIAL_CONCURRENCY = 4;

  private ISqLiteJsonCacheManager Create(string path, int concurrency) =>
    SqLiteJsonCacheManager.FromFilePath(path, concurrency);

  public ISqLiteJsonCacheManager CreateForUser(string scope) =>
    Create(Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db"), 1);

  public ISqLiteJsonCacheManager CreateFromStream(string streamId) =>
    Create(modelCacheManager.GetStreamPath(streamId), INITIAL_CONCURRENCY);
}
