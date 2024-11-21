using System.Dynamic;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Scotec.AspNetCore.DbContextWatcher;

public class DynamicStateContainer : DynamicObject
{
    private static readonly ConditionalWeakTable<DbContext, DynamicStateContainer> Containers = new();
    private readonly Dictionary<string, object?> _properties = new();

    public static DynamicStateContainer GetContainer(DbContext instance)
    {
        return Containers.GetOrCreateValue(instance);
    }

    // Override TrySetMember to set dynamic properties
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _properties[binder.Name] = value;
        return true;
    }

    // Override TryGetMember to get dynamic properties
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (_properties.TryGetValue(binder.Name, out result))
        {
            return true;
        }

        result = null;
        return false;
    }
}