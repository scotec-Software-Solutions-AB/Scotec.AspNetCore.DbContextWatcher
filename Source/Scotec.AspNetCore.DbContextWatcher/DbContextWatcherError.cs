namespace Scotec.AspNetCore.DbContextWatcher;

public enum DbContextWatcherError
{
    /// <summary>
    ///     The DbContext contains changes that have not yet been sent to the database.
    /// </summary>
    UnsafedData,

    /// <summary>
    ///     The DbContext contains modified data. However, changes are not permitted as the session is in a read-only context.
    /// </summary>
    ModifiedData,

    /// <summary>
    /// Saving changes is not permitted as the session is in the read-only context.
    /// </summary>
    Forbidden
}