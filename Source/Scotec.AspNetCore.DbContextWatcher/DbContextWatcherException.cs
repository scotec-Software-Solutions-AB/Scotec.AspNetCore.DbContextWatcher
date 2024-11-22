namespace Scotec.AspNetCore.DbContextWatcher;

public class DbContextWatcherException : OperationCanceledException
{
    public DbContextWatcherException(DbContextWatcherError cause)
        : this(cause, new CancellationToken(true))
    {
    }

    public DbContextWatcherException(DbContextWatcherError cause, CancellationToken cancellationToken)
        : base(cancellationToken)
    {
        Cause = cause;
    }

    public DbContextWatcherError Cause { get; }
}