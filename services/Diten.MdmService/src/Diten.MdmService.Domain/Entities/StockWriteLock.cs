using System.Collections.Concurrent;
using System.Reflection;

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// MOD-0290-FU04 S5b-FIX3 (CT decision) — marks a field of a stock-bearing record (GSKU, LSKU, Finished Good) whose
/// non-null value holds the record against a stock-behaviour write: a lifecycle operation's binding, a retirement, a
/// draft cancellation. The lock lists are built from this attribute (<see cref="StockWriteLocks.Of{T}"/>) — the write's
/// Mongo filter (each field null or missing), its refusal reading and the detail's editable flag all read that ONE
/// list; no list is written by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class StockWriteLockAttribute : Attribute;

/// <summary>The lock fields of a record type, read from <see cref="StockWriteLockAttribute"/>.</summary>
public static class StockWriteLocks
{
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> Cache = new();

    /// <summary>The stored names of the type's lock fields, in name order.</summary>
    public static IReadOnlyList<string> Of<T>() => Properties(typeof(T)).Select(x => x.Name).ToArray();

    /// <summary>Whether any lock field of the record holds a value (the in-memory reading of the write's filter).</summary>
    public static bool Held<T>(T record) where T : class =>
        Properties(typeof(T)).Any(property => property.GetValue(record) is not null);

    private static IReadOnlyList<PropertyInfo> Properties(Type type) => Cache.GetOrAdd(type, static type => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.IsDefined(typeof(StockWriteLockAttribute), inherit: true))
        .OrderBy(property => property.Name, StringComparer.Ordinal)
        .ToArray());
}
