using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL;
using Speckle.Sdk.Api.GraphQL.Enums;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Api.GraphQL.Models.Responses;
using Speckle.Sdk.Api.GraphQL.Resources;
using Speckle.Sdk.Bundles;
using Speckle.Sdk.Credentials;
using Version = Speckle.Sdk.Api.GraphQL.Models.Version;

namespace Speckle.Sdk.Tests.Unit.Api.GraphQL;

/// <summary>The readiness step after a version-creating call: poll the ingestion to a terminal status.</summary>
public sealed class ModelIngestionWaitTests
{
  private static ModelIngestion Ingestion(ModelIngestionStatus status, string? versionId = null) =>
    new()
    {
      id = "ing-1",
      createdAt = DateTime.UtcNow,
      updatedAt = DateTime.UtcNow,
      modelId = "model",
      projectId = "proj",
      userId = "user",
      cancellationRequested = false,
      versionId = "ver",
      statusData = new()
      {
        status = status,
        progressMessage = status == ModelIngestionStatus.failed ? "boom" : null,
        versionId = versionId,
      },
    };

  /// <summary>Serves a scripted sequence of ingestion states, one per Get.</summary>
  private sealed class SequencedGraphQLClient(params ModelIngestion[] states) : ISpeckleGraphQLClient
  {
    public int Calls { get; private set; }

    Task<T> ISpeckleGraphQLClient.ExecuteGraphQLRequest<T>(GraphQLRequest request, CancellationToken ct)
    {
      var state = states[Math.Min(Calls, states.Length - 1)];
      Calls++;
      object response = new RequiredResponse<RequiredResponse<ModelIngestion>>(new(state));
      return Task.FromResult((T)response);
    }

    IDisposable ISpeckleGraphQLClient.SubscribeTo<T>(GraphQLRequest request, Action<object, T> callback) =>
      throw new NotSupportedException();
  }

  [Fact]
  public async Task WaitForCompletion_PollsUntilTerminal()
  {
    var gql = new SequencedGraphQLClient(
      Ingestion(ModelIngestionStatus.queued),
      Ingestion(ModelIngestionStatus.processing),
      Ingestion(ModelIngestionStatus.success, "ver")
    );
    var resource = new ModelIngestionResource(gql);

    var result = await resource.WaitForCompletionAsync(
      "ing-1",
      "proj",
      timeout: TimeSpan.FromSeconds(30),
      pollInterval: TimeSpan.FromMilliseconds(1)
    );

    Assert.Equal(ModelIngestionStatus.success, result.statusData.status);
    Assert.Equal("ver", result.statusData.versionId);
    Assert.Equal(3, gql.Calls);
  }

  [Theory]
  [InlineData(ModelIngestionStatus.failed)]
  [InlineData(ModelIngestionStatus.cancelled)]
  [InlineData(ModelIngestionStatus.invalidInput)]
  [InlineData(ModelIngestionStatus.timeout)]
  public async Task WaitForCompletion_ReturnsNonSuccessTerminals_ForTheCallerToInspect(ModelIngestionStatus terminal)
  {
    var resource = new ModelIngestionResource(new SequencedGraphQLClient(Ingestion(terminal)));

    var result = await resource.WaitForCompletionAsync("ing-1", "proj", pollInterval: TimeSpan.FromMilliseconds(1));

    Assert.Equal(terminal, result.statusData.status);
  }

  [Fact]
  public async Task WaitForCompletion_Timeout_ThrowsWithLastStatus()
  {
    var resource = new ModelIngestionResource(new SequencedGraphQLClient(Ingestion(ModelIngestionStatus.processing)));

    var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
      resource.WaitForCompletionAsync(
        "ing-1",
        "proj",
        timeout: TimeSpan.FromMilliseconds(20),
        pollInterval: TimeSpan.FromMilliseconds(10)
      )
    );
    Assert.Contains("processing", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Operations_WaitForVersion_DelegatesToTheBundleSender()
  {
    var sender = new Mock<IBundleSender>(MockBehavior.Strict);
    var services = new ServiceCollection();
    services.AddSpeckleSdk(new("Tests", "test"), "v3", typeof(ModelIngestionWaitTests).Assembly);
    services.AddSingleton(sender.Object);
    var operations = services.BuildServiceProvider().GetRequiredService<IOperations>();

    var account = new Account
    {
      token = "tok",
      serverInfo = new() { url = "https://example.speckle.invalid/" },
      userInfo = new(),
    };
    var sent = new SendResult("proj", "model", "ver", "ing-1", 1);
    var version = new Version
    {
      id = "ver",
      referencedObject = "bundle.proj.model.ver",
      message = null,
      sourceApplication = null,
      createdAt = DateTime.UtcNow,
      previewUrl = new Uri("https://example.speckle.invalid/preview"),
      authorUser = null,
    };
    sender
      .Setup(s => s.WaitForVersionAsync(account, sent, TimeSpan.FromSeconds(5), It.IsAny<CancellationToken>()))
      .ReturnsAsync(version);

    var result = await operations.WaitForVersion(account, sent, TimeSpan.FromSeconds(5), CancellationToken.None);

    Assert.Same(version, result);
    sender.VerifyAll();
  }
}
