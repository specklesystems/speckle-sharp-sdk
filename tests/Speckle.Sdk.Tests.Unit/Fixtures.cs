using Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit;

public abstract class Fixtures
{
  private static readonly SQLiteTransport s_accountStorage = new(scope: "Accounts");

  public static void UpdateOrSaveAccount(Account account)
  {
    DeleteLocalAccount(account.id.NotNull());
    string serializedObject = JsonConvert.SerializeObject(account);
    s_accountStorage.SaveObjectSync(account.id, serializedObject);
  }

  public static void DeleteLocalAccount(string id) => s_accountStorage.DeleteObject(id);
}
