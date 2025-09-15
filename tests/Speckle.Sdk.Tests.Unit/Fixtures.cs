using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.SQLite;
using Speckle.Sdk.Tests.Unit.Host;
using Xunit.Sdk;

namespace Speckle.Sdk.Tests.Unit;

public abstract class Fixtures
{
  static Fixtures()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(TestClass).Assembly, typeof(Polyline).Assembly);
    var serviceProvider = serviceCollection.BuildServiceProvider();

    var factory = serviceProvider.GetRequiredService<ISqLiteJsonCacheManagerFactory>();
    s_accountStorage = factory.CreateForUser("Accounts");
  }

  private static readonly ISqLiteJsonCacheManager s_accountStorage;

  private static readonly string s_accountPath = Path.Combine(
    SpecklePathProvider.AccountsFolderPath,
    "TestAccount.json"
  );

  public static void UpdateOrSaveAccount(Account account)
  {
    DeleteLocalAccount(account.id.NotNull());
    string serializedObject = JsonConvert.SerializeObject(account);
    s_accountStorage.SaveObject(account.id, serializedObject);
  }

  public static void SaveLocalAccount(Account account)
  {
    var json = JsonConvert.SerializeObject(account);
    File.WriteAllText(s_accountPath, json);
  }

  public static void DeleteLocalAccount(string id) => s_accountStorage.DeleteObject(id);

  public static void DeleteLocalAccountFile() => File.Delete(s_accountPath);
}
