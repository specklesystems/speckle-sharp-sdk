using Speckle.Sdk.Transports;


namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public partial class OperationsReceiveTests
{
  [Theory, MemberData(nameof(TestCases))]
  public async Task Receive_ObjectsDontExist_ExceptionThrown(string id)
  {
    MemoryTransport emptyTransport1 = new();
    MemoryTransport emptyTransport2 = new();
    await Assert.ThrowsAsync<TransportException>(async () =>
    {
      await _operations.Receive(id, emptyTransport1, emptyTransport2);
    });
  }

  [Theory, MemberData(nameof(TestCases))]
  public async Task Receive_ObjectsDontExistNullRemote_ExceptionThrown(string id)
  {
    MemoryTransport emptyTransport = new();
    await Assert.ThrowsAsync<TransportException>(async () =>
    {
      await _operations.Receive(id, null, emptyTransport);
    });
  }

  [Theory, MemberData(nameof(TestCases))]
  public async Task Receive_OperationCanceled_ExceptionThrown(string id)
  {
    using CancellationTokenSource ctc = new();
    ctc.Cancel();

    MemoryTransport emptyTransport2 = new();
    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
      await _operations.Receive(id, _testCaseTransport, emptyTransport2, cancellationToken: ctc.Token);
    });
  }
}
