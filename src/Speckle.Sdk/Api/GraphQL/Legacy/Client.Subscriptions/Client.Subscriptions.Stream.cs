#nullable disable
using Speckle.Sdk.Api.GraphQL.Resources;

namespace Speckle.Sdk.Api;

public partial class Client
{
  [Obsolete($"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateUserProjectsUpdatedSubscription)}", true)]
  public void SubscribeUserStreamAdded() => throw new NotImplementedException();

  [Obsolete($"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectUpdatedSubscription)}", true)]
  public void SubscribeStreamUpdated(string id) => throw new NotImplementedException();

  [Obsolete($"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateUserProjectsUpdatedSubscription)}", true)]
  public void SubscribeUserStreamRemoved() => throw new NotImplementedException();

  [Obsolete(
    $"Use {nameof(Subscription)}.{nameof(SubscriptionResource.CreateProjectCommentsUpdatedSubscription)}",
    true
  )]
  public void SubscribeCommentActivity(string streamId) => throw new NotImplementedException();
}
