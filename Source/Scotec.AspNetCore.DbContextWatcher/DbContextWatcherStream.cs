namespace Scotec.AspNetCore.DbContextWatcher;

internal class DbContextWatcherStream : Stream
{
    private readonly Func<bool> _hasChanges;
    private readonly Func<bool> _canSave;
    private readonly Stream _responseBodyStream;

    public DbContextWatcherStream(Stream responseBodyStream, Func<bool> hasChanges, Func<bool> canSave)
    {
        _responseBodyStream = responseBodyStream;
        _hasChanges = hasChanges;
        _canSave = canSave;
    }

    public override bool CanRead => _responseBodyStream.CanRead;

    public override bool CanSeek => _responseBodyStream.CanSeek;

    public override bool CanTimeout => _responseBodyStream.CanTimeout;

    public override bool CanWrite => _responseBodyStream.CanWrite;

    public override long Length => _responseBodyStream.Length;

    public override long Position
    {
        get => _responseBodyStream.Position;
        set => _responseBodyStream.Position = value;
    }

    public override int ReadTimeout
    {
        get => _responseBodyStream.ReadTimeout;
        set => _responseBodyStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _responseBodyStream.WriteTimeout;
        set => _responseBodyStream.WriteTimeout = value;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return _responseBodyStream.BeginRead(buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        TestDbContext();
        return _responseBodyStream.BeginWrite(buffer, offset, count, callback, state);
    }

    public override void Close()
    {
        _responseBodyStream.Close();
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        TestDbContext();
        _responseBodyStream.CopyTo(destination, bufferSize);
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        TestDbContext(cancellationToken);
        return _responseBodyStream.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override ValueTask DisposeAsync()
    {
        return _responseBodyStream.DisposeAsync();
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return _responseBodyStream.EndRead(asyncResult);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        _responseBodyStream.EndWrite(asyncResult);
    }

    public override void Flush()
    {
        _responseBodyStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _responseBodyStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _responseBodyStream.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        return _responseBodyStream.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _responseBodyStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new())
    {
        return _responseBodyStream.ReadAsync(buffer, cancellationToken);
    }

    public override int ReadByte()
    {
        return _responseBodyStream.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _responseBodyStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _responseBodyStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        TestDbContext();
        _responseBodyStream.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        TestDbContext();
        _responseBodyStream.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        TestDbContext(cancellationToken);
        return _responseBodyStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new())
    {
        TestDbContext(cancellationToken);
        await _responseBodyStream.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        TestDbContext();
        _responseBodyStream.WriteByte(value);
    }

    private void TestDbContext()
    {
        TestDbContext(new CancellationToken(true));
    }

    /// <summary>
    /// Check whether the DbContext contains modified data. All changes must be saved before the response is sent back to the client. In the read-only context, the DbContext must generally not contain any changes.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <exception cref="DbContextWatcherException"></exception>
    private void TestDbContext(CancellationToken cancellationToken)
    {
        if (_hasChanges())
        {
            throw _canSave() 
                ? new DbContextWatcherException(DbContextWatcherError.UnsafedData, cancellationToken)
                : new DbContextWatcherException(DbContextWatcherError.ModifiedData, cancellationToken);
        }
    }
}