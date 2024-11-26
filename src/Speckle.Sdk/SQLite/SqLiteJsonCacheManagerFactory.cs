using Speckle.InterfaceGenerator;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.SQLite;

[GenerateAutoInterface]
public class SqLiteJsonCacheManagerFactory : ISqLiteJsonCacheManagerFactory
{
  public ISqLiteJsonCacheManager Create(string path) => new SqLiteJsonCacheManager(path);

  public ISqLiteJsonCacheManager CreateFromStream(string streamId) =>
    new SqLiteJsonCacheManager(SqlitePaths.GetDBPath(streamId));
}
