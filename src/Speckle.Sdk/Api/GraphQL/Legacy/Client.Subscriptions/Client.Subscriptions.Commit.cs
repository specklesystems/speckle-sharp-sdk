#nullable disable
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Api;

public partial class Client
{
  [Obsolete(
    $"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectVersionsUpdatedSubscription)}",
    true
  )]
  public void SubscribeCommitCreated(string streamId) => throw new NotImplementedException();

  [Obsolete(
    $"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectVersionsUpdatedSubscription)}",
    true
  )]
  public void SubscribeCommitUpdated(string streamId, string commitId = null) => throw new NotImplementedException();

  [Obsolete(
    $"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectVersionsUpdatedSubscription)}",
    true
  )]
  public void SubscribeCommitDeleted(string streamId) => throw new NotImplementedException();
}
