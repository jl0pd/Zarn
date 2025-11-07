namespace StreamRpc.AspNetCore;

// NOTE: this type doesn't own given streams, so it doesn't disposes them
internal sealed class AspNetCombinedStream(Stream readStream, Stream writeStream) : Stream
{
    protected override void Dispose(bool disposing) => base.Dispose(disposing);

    public override ValueTask DisposeAsync() => base.DisposeAsync();

    public override bool CanTimeout => readStream.CanTimeout || writeStream.CanTimeout;

    #region write
    public override bool CanWrite => true;

    public override void Flush() => writeStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => writeStream.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => writeStream.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => writeStream.WriteAsync(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => writeStream.WriteAsync(buffer, cancellationToken);

    public override void WriteByte(byte value) => writeStream.WriteByte(value);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => writeStream.BeginWrite(buffer, offset, count, callback, state);

    public override void EndWrite(IAsyncResult asyncResult) => writeStream.EndWrite(asyncResult);

    public override int WriteTimeout
    {
        get => writeStream.WriteTimeout;
        set => writeStream.WriteTimeout = value;
    }

    #endregion write

    #region read
    public override bool CanRead => true;

    public override int Read(byte[] buffer, int offset, int count) => readStream.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => readStream.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => readStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => readStream.ReadAsync(buffer, cancellationToken);

    public override int ReadByte() => readStream.ReadByte();

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => readStream.BeginRead(buffer, offset, count, callback, state);

    public override int EndRead(IAsyncResult asyncResult) => readStream.EndRead(asyncResult);

    public override int ReadTimeout
    {
        get => readStream.ReadTimeout;
        set => readStream.ReadTimeout = value;
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => readStream.CopyToAsync(destination, bufferSize, cancellationToken);

    public override void CopyTo(Stream destination, int bufferSize) => readStream.CopyTo(destination, bufferSize);

    #endregion read

    #region unsupported

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    #endregion unsupported
}
