namespace Speckle.Sdk.Helpers;

public class ProgressMessageHandler : DelegatingHandler
{
  /// <summary>
  /// Initializes a new instance of the <see cref="ProgressMessageHandler"/> class.
  /// </summary>
  public ProgressMessageHandler()
  {
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="ProgressMessageHandler"/> class.
  /// </summary>
  /// <param name="innerHandler">The inner handler to which this handler submits requests.</param>
  public ProgressMessageHandler(HttpMessageHandler innerHandler)
    : base(innerHandler)
  {
  }

  /// <summary>
  /// Occurs every time the client sending data is making progress.
  /// </summary>
  public event EventHandler<HttpProgressEventArgs>? HttpSendProgress;

  /// <summary>
  /// Occurs every time the client receiving data is making progress.
  /// </summary>
  public event EventHandler<HttpProgressEventArgs>? HttpReceiveProgress;
  
  /// <summary>
  /// Raises the <see cref="HttpSendProgress"/> event.
  /// </summary>
  /// <param name="request">The request.</param>
  /// <param name="e">The <see cref="HttpProgressEventArgs"/> instance containing the event data.</param>
  protected internal virtual void OnHttpRequestProgress(HttpRequestMessage request, HttpProgressEventArgs e)
  {
    if (HttpSendProgress != null)
    {
      HttpSendProgress(request, e);
    }
  }

  /// <summary>
  /// Raises the <see cref="HttpReceiveProgress"/> event.
  /// </summary>
  /// <param name="request">The request.</param>
  /// <param name="e">The <see cref="HttpProgressEventArgs"/> instance containing the event data.</param>
  protected internal virtual void OnHttpResponseProgress(HttpRequestMessage request, HttpProgressEventArgs e)
  {
    if (HttpReceiveProgress != null)
    {
      HttpReceiveProgress(request, e);
    }
  }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    AddRequestProgress(request);
    HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

    if (HttpReceiveProgress != null && response.Content != null)
    {
      cancellationToken.ThrowIfCancellationRequested();
      await AddResponseProgressAsync(request, response);
    }

    return response;
  }


  private void AddRequestProgress(HttpRequestMessage request)
  {
    if (HttpSendProgress != null &&  request.Content != null)
    {
      HttpContent progressContent = new ProgressContent(request.Content, this, request);
      request.Content = progressContent;
    }
  }

  private async Task<HttpResponseMessage> AddResponseProgressAsync(HttpRequestMessage request, HttpResponseMessage response)
  {
    Stream stream = await response.Content.ReadAsStreamAsync();
    ProgressStream progressStream = new(stream, this, request, response);
    HttpContent progressContent = new StreamContent(progressStream);
    response.Content.Headers.CopyTo(progressContent.Headers);
    response.Content = progressContent;
    return response;
  }
}

internal class ProgressStream : Stream
{
  private readonly Stream InnerStream;
        private readonly ProgressMessageHandler _handler;
        private readonly HttpRequestMessage _request;

        private long _bytesReceived;
        private long? _totalBytesToReceive;

        private long _bytesSent;
        private long? _totalBytesToSend;

        public ProgressStream(Stream innerStream, ProgressMessageHandler handler, HttpRequestMessage request, HttpResponseMessage? response)
        {
          InnerStream = innerStream;

            if (request.Content != null)
            {
                _totalBytesToSend = request.Content.Headers.ContentLength;
            }

            if (response != null && response.Content != null)
            {
                _totalBytesToReceive = response.Content.Headers.ContentLength;
            }

            _handler = handler;
            _request = request;
        }

        public override void Flush() => InnerStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = InnerStream.Read(buffer, offset, count);
            ReportBytesReceived(bytesRead, userState: null);
            return bytesRead;
        }

        public override int ReadByte()
        {
            int byteRead = InnerStream.ReadByte();
            ReportBytesReceived(byteRead == -1 ? 0 : 1, userState: null);
            return byteRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int readCount = await InnerStream.ReadAsync(buffer, offset, count, cancellationToken);
            ReportBytesReceived(readCount, userState: null);
            return readCount;
        }


        public override void Write(byte[] buffer, int offset, int count)
        {
            InnerStream.Write(buffer, offset, count);
            ReportBytesSent(count, userState: null);
        }

        public override void WriteByte(byte value)
        {
            InnerStream.WriteByte(value);
            ReportBytesSent(1, userState: null);
        }

        public override bool CanRead => InnerStream.CanRead;
        public override bool CanSeek  => InnerStream.CanSeek;
        public override bool CanWrite  => InnerStream.CanWrite;
        public override long Length  => InnerStream.Length;
        public override long Position { get => InnerStream.Position; set => InnerStream.Position = value; }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await InnerStream.WriteAsync(buffer, offset, count, cancellationToken);
            ReportBytesSent(count, userState: null);
        }


        internal void ReportBytesSent(int bytesSent, object? userState)
        {
            if (bytesSent > 0)
            {
                _bytesSent += bytesSent;
                int percentage = 0;
                if (_totalBytesToSend.HasValue && _totalBytesToSend != 0)
                {
                    percentage = (int)((100L * _bytesSent) / _totalBytesToSend.Value);
                }

                // We only pass the request as it is guaranteed to be non-null (the response may be null)
                _handler.OnHttpRequestProgress(_request, new HttpProgressEventArgs(percentage, userState, _bytesSent, _totalBytesToSend));
            }
        }

        private void ReportBytesReceived(int bytesReceived, object? userState)
        {
            if (bytesReceived > 0)
            {
                _bytesReceived += bytesReceived;
                int percentage = 0;
                if (_totalBytesToReceive.HasValue && _totalBytesToReceive != 0)
                {
                    percentage = (int)((100L * _bytesReceived) / _totalBytesToReceive.Value);
                }

                // We only pass the request as it is guaranteed to be non-null (the response may be null)
                _handler.OnHttpResponseProgress(_request, new HttpProgressEventArgs(percentage, userState, _bytesReceived, _totalBytesToReceive));
            }
        }
    }
