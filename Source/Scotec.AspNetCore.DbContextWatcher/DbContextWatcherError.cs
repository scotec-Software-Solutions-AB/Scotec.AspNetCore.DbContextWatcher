namespace Scotec.AspNetCore.DbContextWatcher;

public enum DbContextWatcherError
{
    /// <summary>
    ///     The DbContext contains changes that have not yet been sent to the database.
    /// </summary>
    UnsafedData,

    /// <summary>
    ///     The DbContext contains modified data that cannot be saved as the current session is in a read-only context.
    /// </summary>
    ModifiedData
}