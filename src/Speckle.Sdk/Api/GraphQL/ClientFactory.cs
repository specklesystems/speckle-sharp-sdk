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
  public Client Create(Account account) =>
    new(loggerFactory.CreateLogger<Client>(), activityFactory, application, speckleHttp, account);
}
