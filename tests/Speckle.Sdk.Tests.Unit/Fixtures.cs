using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit;

public abstract class Fixtures
{
  private static readonly SQLiteTransport s_accountStorage = new(scope: "Accounts");

  private static readonly string s_accountPath = Path.Combine(
    SpecklePathProvider.AccountsFolderPath,
    "TestAccount.json"
  );

  public static void UpdateOrSaveAccount(Account account)
  {
    DeleteLocalAccount(account.id.NotNull());
    string serializedObject = JsonConvert.SerializeObject(account);
    s_accountStorage.SaveObjectSync(account.id, serializedObject);
  }

  public static void SaveLocalAccount(Account account)
  {
    var json = JsonConvert.SerializeObject(account);
    File.WriteAllText(s_accountPath, json);
  }

  public static void DeleteLocalAccount(string id) => s_accountStorage.DeleteObject(id);

  public static void DeleteLocalAccountFile() => File.Delete(s_accountPath);
}
