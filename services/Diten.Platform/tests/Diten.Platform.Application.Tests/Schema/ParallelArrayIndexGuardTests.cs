using System.Collections;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.Platform.Application.Tests.Schema;

/// <summary>
/// BL-526 — no index in the manifest holds two fields that are stored as arrays.
///
/// <para>MongoDB refuses to index a document in which two fields of one compound index are BOTH arrays ("cannot
/// index parallel arrays", code 171), and it refuses at WRITE time — the index builds fine on an empty collection
/// and on documents where one of the two is missing, so nothing fails until the first document carries both. With
/// no <c>DateTimeOffset</c> serializer registered (BL-030) every <c>DateTimeOffset</c> is stored as
/// <c>[ticks, offsetMinutes]</c>: an index over two dates makes every row that fills the second date unwritable. That
/// is how a seat could be opened but never ended (<c>ix_position_assignments_*_interval</c>).</para>
///
/// <para>Measured from the production manifest and the production class maps; a field counts as an array when its
/// CLR type is <c>DateTimeOffset</c> / <c>DateTimeOffset?</c> or a collection other than <c>string</c> / <c>byte[]</c>.</para>
/// </summary>
public sealed class ParallelArrayIndexGuardTests
{
    [Fact]
    public void No_manifest_index_holds_two_fields_stored_as_arrays()
    {
        PlatformTestSerializers.Register();

        var offenders = PlatformSchemaManifest.All
            .SelectMany(collection => collection.Indexes.Select(index => (collection, index)))
            .Select(pair => (pair.collection, pair.index, arrays: pair.index.Key.Names
                .Where(path => IsStoredAsArray(pair.collection.DocumentType, path))
                .ToList()))
            .Where(x => x.arrays.Count >= 2)
            .Select(x => $"{x.collection.Name}.{x.index.Name}: {string.Join(", ", x.arrays)}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These indexes hold two fields stored as arrays; the first document that fills both is refused with "
            + "'cannot index parallel arrays':\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void The_guard_sees_a_date_as_an_array_and_reads_the_seat_indexes()
    {
        var seats = PlatformSchemaManifest.All.Single(c => c.Name == PlatformCollections.PositionAssignments);
        Assert.True(IsStoredAsArray(seats.DocumentType, "EffectiveFrom"));
        Assert.True(IsStoredAsArray(seats.DocumentType, "EffectiveTo"));
        Assert.False(IsStoredAsArray(seats.DocumentType, "TenantId"));
        Assert.NotEmpty(seats.Indexes);
    }

    private static bool IsStoredAsArray(Type documentType, string path)
    {
        var type = documentType;
        foreach (var segment in path.Split('.'))
        {
            var member = FindMember(type, segment);
            if (member is null)
            {
                return false; // not a mapped member (e.g. a positional or dynamic key): not judged here
            }

            type = member;
            if (IsArrayShaped(type))
            {
                return true;
            }
        }

        return false;
    }

    private static Type? FindMember(Type type, string elementName)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            var map = BsonClassMap.LookupClassMap(current);
            var member = map.DeclaredMemberMaps.FirstOrDefault(m => m.ElementName == elementName);
            if (member is not null)
            {
                return member.MemberType;
            }
        }

        return null;
    }

    private static bool IsArrayShaped(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(DateTimeOffset))
        {
            return true;
        }

        return underlying != typeof(string)
               && underlying != typeof(byte[])
               && typeof(IEnumerable).IsAssignableFrom(underlying);
    }
}
