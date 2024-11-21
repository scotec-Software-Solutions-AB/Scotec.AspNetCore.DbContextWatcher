using Microsoft.EntityFrameworkCore;

// ReSharper disable InconsistentNaming

namespace Scotec.AspNetCore.DbContextWatcher;

public static class DbContextPatch<TDbContext> where TDbContext : DbContext
{
    public static bool Prefix(TDbContext __instance)
    {
        dynamic container = DynamicStateContainer.GetContainer(__instance);
        if (!container.CanSaveChanges)
        {
            throw new DbContextWatcherException(DbContextWatcherError.ModifiedData);
        }

        return true;
    }
}