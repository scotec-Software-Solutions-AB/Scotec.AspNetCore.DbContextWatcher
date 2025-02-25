using Microsoft.CSharp.RuntimeBinder;
using Microsoft.EntityFrameworkCore;

// ReSharper disable InconsistentNaming

namespace Scotec.AspNetCore.DbContextWatcher;

public static class DbContextWatcherPatch<TDbContext> where TDbContext : DbContext
{
    public static bool Prefix(TDbContext __instance)
    {
        try
        {
            dynamic container = DynamicStateContainer.GetContainer(__instance);
            if (container.CanSaveChanges() is Task<bool> task)
            {
                if (!task.GetAwaiter().GetResult())
                {
                    throw new DbContextWatcherException(DbContextWatcherError.Forbidden);
                }
            }
          
            return true;

        }
        catch (RuntimeBinderException)
        {
            // Do nothing.
        }

        return true;
    }
}