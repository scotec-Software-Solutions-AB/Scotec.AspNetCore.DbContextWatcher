﻿using Microsoft.EntityFrameworkCore;

// ReSharper disable InconsistentNaming

namespace Scotec.AspNetCore.DbContextWatcher;

public static class DbContextWatcherPatch<TDbContext> where TDbContext : DbContext
{
    public static bool Prefix(TDbContext __instance)
    {
        dynamic container = DynamicStateContainer.GetContainer(__instance);
        if (!((Task<bool>)container.CanSaveChanges()).GetAwaiter().GetResult())
        {
            throw new DbContextWatcherException(DbContextWatcherError.Forbidden);
        }

        return true;
    }
}