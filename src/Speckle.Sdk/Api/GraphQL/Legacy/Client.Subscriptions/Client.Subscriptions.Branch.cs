#nullable disable
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Api;

public partial class Client
{
  [Obsolete($"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectModelsUpdatedSubscription)}", true)]
  public void SubscribeBranchCreated(string streamId) => throw new NotImplementedException();

  [Obsolete($"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectModelsUpdatedSubscription)}", true)]
  public void SubscribeBranchUpdated(string streamId, string branchId = null) => throw new NotImplementedException();

  [Obsolete($"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectModelsUpdatedSubscription)}", true)]
  public void SubscribeBranchDeleted(string streamId) => throw new NotImplementedException();
}
