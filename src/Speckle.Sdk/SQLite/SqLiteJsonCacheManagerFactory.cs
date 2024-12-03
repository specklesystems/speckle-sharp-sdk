﻿using Speckle.InterfaceGenerator;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.SQLite;

[GenerateAutoInterface]
public class SqLiteJsonCacheManagerFactory : ISqLiteJsonCacheManagerFactory
{
  private ISqLiteJsonCacheManager Create(string path) => new SqLiteJsonCacheManager(path);

  public ISqLiteJsonCacheManager CreateForUser(string scope) =>
    Create(Path.Combine(SpecklePathProvider.UserApplicationDataPath(), "Speckle", $"{scope}.db"));

  public ISqLiteJsonCacheManager CreateFromStream(string streamId) => Create(SqlitePaths.GetDBPath(streamId));
}
