using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Serialization.Tests;

public class SqliteManagerTests
{
  [Test]
  public void CheckSqlite_Cache_Receieve()
  {
    var path = Path.GetTempPath();
    var app = Guid.NewGuid().ToString();
    var scope = "data";
    var options = new SqliteManagerOptions(true, path, app, scope);
    var id = Guid.NewGuid().ToString();
    var json = new JObject();
    json["id"] = id;

    using (var sqliteManager = new SqliteManager(options))
    {
      sqliteManager.SaveObjects(new List<(string, string)>() { (id, json.ToString()) }, default);
    }

    List<(string, string?)> results;
    using (var sqliteManager = new SqliteManager(options))
    {
      results = sqliteManager.GetObjects([id], default).ToList();
    }
    results.Count.ShouldBe(1);
    var root = JObject.Parse(results.First().Item2.NotNull());
    root["id"].ShouldBe(id);

    var fullPath = Path.Combine(path, app, $"{scope}.db");
    File.Exists(fullPath).ShouldBeTrue();
    //can't delete this because it's pooled https://github.com/dotnet/efcore/issues/27139
    //File.Delete(fullPath);
  }
}
