namespace StreamRpc.Utils;

internal sealed class UnclosableStream(Stream underlying) : Stream
{
    public override bool CanRead => underlying.CanRead;

    public override bool CanSeek => underlying.CanSeek;

    public override bool CanWrite => underlying.CanWrite;

    public override long Length => underlying.Length;

    public override long Position { get => underlying.Position; set => underlying.Position = value; }

    public override void Flush() => underlying.Flush();

    public override int Read(byte[] buffer, int offset, int count) => underlying.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => underlying.Seek(offset, origin);

    public override void SetLength(long value) => underlying.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => underlying.Write(buffer, offset, count);

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => underlying.BeginRead(buffer, offset, count, callback, state);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => underlying.BeginWrite(buffer, offset, count, callback, state);

    public override bool CanTimeout => underlying.CanTimeout;

    protected override void Dispose(bool disposing) => base.Dispose(disposing);

    public override ValueTask DisposeAsync() => base.DisposeAsync();

    public override void CopyTo(Stream destination, int bufferSize) => underlying.CopyTo(destination, bufferSize);

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => underlying.CopyToAsync(destination, bufferSize, cancellationToken);

    public override int EndRead(IAsyncResult asyncResult) => underlying.EndRead(asyncResult);

    public override void EndWrite(IAsyncResult asyncResult) => underlying.EndWrite(asyncResult);

    public override Task FlushAsync(CancellationToken cancellationToken) => underlying.FlushAsync(cancellationToken);

    public override int Read(Span<byte> buffer) => underlying.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => underlying.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => underlying.ReadAsync(buffer, cancellationToken);

    public override int ReadTimeout { get => underlying.ReadTimeout; set => underlying.ReadTimeout = value; }

    public override int ReadByte() => underlying.ReadByte();

    public override void Write(ReadOnlySpan<byte> buffer) => underlying.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => underlying.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => underlying.WriteAsync(buffer, cancellationToken);

    public override void WriteByte(byte value) => underlying.WriteByte(value);

    public override int WriteTimeout { get => underlying.WriteTimeout; set => underlying.WriteTimeout = value; }
}
