using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Speckle.Sdk.Tests.Unit.Api.GraphQL;

public enum SocketTeardown
{
  /// <summary>Severs the connection without a close frame, as an unfixed server does on pod shutdown.</summary>
  Abort,

  /// <summary>Sends a 1001 close frame, as a fixed server would.</summary>
  CleanClose,
}

/// <summary>
/// A minimal graphql-ws endpoint that accepts a subscription and then drops the socket, so a client's
/// reconnect behaviour can be observed as a second connection arriving.
/// </summary>
public sealed class FakeGraphQLWebSocketServer : IDisposable
{
  private readonly HttpListener _listener;
  private readonly CancellationTokenSource _cts = new();
  private readonly SemaphoreSlim _connectionSignal = new(0);
  private readonly SocketTeardown _teardown;
  private int _connectionCount;

  public FakeGraphQLWebSocketServer(SocketTeardown teardown)
  {
    _teardown = teardown;
    Port = GetFreePort();
    _listener = new HttpListener();
    _listener.Prefixes.Add($"http://localhost:{Port}/");
    _listener.Start();
    _ = Task.Run(AcceptLoopAsync);
  }

  public int Port { get; }

  public Uri ServerUrl => new($"http://localhost:{Port}");

  public int ConnectionCount => Volatile.Read(ref _connectionCount);

  public async Task<bool> WaitForConnectionsAsync(int count, TimeSpan timeout)
  {
    using var timeoutCts = new CancellationTokenSource(timeout);
    for (int i = 0; i < count; i++)
    {
      try
      {
        await _connectionSignal.WaitAsync(timeoutCts.Token);
      }
      catch (OperationCanceledException)
      {
        return false;
      }
    }

    return true;
  }

  private async Task AcceptLoopAsync()
  {
    while (!_cts.IsCancellationRequested)
    {
      HttpListenerContext context;
      try
      {
        context = await _listener.GetContextAsync();
      }
      catch (HttpListenerException)
      {
        return;
      }
      catch (ObjectDisposedException)
      {
        return;
      }
      catch (InvalidOperationException)
      {
        return;
      }

      _ = Task.Run(() => HandleAsync(context));
    }
  }

  private async Task HandleAsync(HttpListenerContext context)
  {
    if (!context.Request.IsWebSocketRequest)
    {
      context.Response.StatusCode = 400;
      context.Response.Close();
      return;
    }

    var wsContext = await context.AcceptWebSocketAsync("graphql-ws");
    var socket = wsContext.WebSocket;
    Interlocked.Increment(ref _connectionCount);
    _connectionSignal.Release();

    try
    {
      await ReceiveAsync(socket);
      await SendAsync(socket, "{\"type\":\"connection_ack\"}");
      await ReceiveAsync(socket);

      if (_teardown == SocketTeardown.Abort)
      {
        socket.Abort();
      }
      else
      {
        await socket.CloseOutputAsync(
          WebSocketCloseStatus.EndpointUnavailable,
          "Server shutting down",
          CancellationToken.None
        );
      }
    }
    catch (WebSocketException)
    {
      // the client may already be gone; nothing to do
    }
    catch (OperationCanceledException)
    {
      // shutting down
    }
    finally
    {
      socket.Dispose();
    }
  }

  private static Task SendAsync(WebSocket socket, string message) =>
    socket.SendAsync(
      new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
      WebSocketMessageType.Text,
      true,
      CancellationToken.None
    );

  private static async Task<string> ReceiveAsync(WebSocket socket)
  {
    var buffer = new byte[8192];
    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    return Encoding.UTF8.GetString(buffer, 0, result.Count);
  }

  private static int GetFreePort()
  {
    using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    return ((IPEndPoint)probe.LocalEndPoint!).Port;
  }

  public void Dispose()
  {
    _cts.Cancel();
    _listener.Close();
    _cts.Dispose();
    _connectionSignal.Dispose();
  }
}
