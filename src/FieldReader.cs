using System;
using System.Linq.Expressions;
using System.Reflection;

namespace StableGame
{
    internal static class FieldReader
    {
        // Build once. Reading value-type fields then needs neither reflection nor boxing.
        internal static Func<TOwner, TValue> Create<TOwner, TValue>(FieldInfo field)
        {
            if (field == null) return null;
            var owner = Expression.Parameter(typeof(TOwner), "owner");
            return Expression.Lambda<Func<TOwner, TValue>>(Expression.Field(owner, field), owner).Compile();
        }
    }
}
