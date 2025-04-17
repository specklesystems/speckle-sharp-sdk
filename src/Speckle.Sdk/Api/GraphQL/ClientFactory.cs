using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Api;

[GenerateAutoInterface]
public class ClientFactory(
  ILoggerFactory loggerFactory,
  ISdkActivityFactory activityFactory,
  ISpeckleApplication application,
  ISpeckleHttp speckleHttp
) : IClientFactory
{
  public IClient Create(Account account) =>
    new Client(loggerFactory.CreateLogger<Client>(), activityFactory, application, speckleHttp, account);

  public IClient Create(Uri serverUrl, string token) =>
    new Client(loggerFactory.CreateLogger<Client>(), activityFactory, application, speckleHttp, serverUrl, token);
}
