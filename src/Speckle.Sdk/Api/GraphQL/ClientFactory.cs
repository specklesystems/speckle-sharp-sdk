using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Api;

[GenerateAutoInterface]
public class ClientFactory(
  ILoggerFactory loggerFactory,
  ISdkActivityFactory activityFactory,
  IGraphQLClientFactory graphQLClientFactory
) : IClientFactory
{
  public IClient Create(Account account) =>
    new Client(loggerFactory.CreateLogger<Client>(), activityFactory, graphQLClientFactory, account);
}
