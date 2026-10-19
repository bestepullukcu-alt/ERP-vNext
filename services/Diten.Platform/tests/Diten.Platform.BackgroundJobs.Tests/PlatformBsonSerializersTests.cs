using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

/// <summary>
/// <see cref="PlatformBsonSerializers"/> is repeatable AND still refuses a different serializer.
///
/// <para>⚠ WHY THE REFUSAL IS DRIVEN THROUGH INJECTED DELEGATES. The driver's registry is process-global and has no
/// way to unregister: really registering a non-Standard Guid serializer here would poison every other test in this
/// process — including the container validation this project exists for. The decision is therefore tested on the
/// production method with its two global effects injected, and the compatible repeat is tested for real.</para>
/// </summary>
public sealed class PlatformBsonSerializersTests
{
    private static readonly Func<IBsonSerializer, bool> IsStandardGuid =
        existing => existing is GuidSerializer { GuidRepresentation: GuidRepresentation.Standard };

    [Fact]
    public void A_second_registration_of_the_same_serializers_in_one_process_is_accepted()
    {
        PlatformBsonSerializers.EnsureRegistered();

        var repeat = Record.Exception(PlatformBsonSerializers.EnsureRegistered);

        Assert.Null(repeat);
        var guid = Assert.IsType<GuidSerializer>(BsonSerializer.LookupSerializer(typeof(Guid)));
        Assert.Equal(GuidRepresentation.Standard, guid.GuidRepresentation);
        var @decimal = Assert.IsType<DecimalSerializer>(BsonSerializer.LookupSerializer(typeof(decimal)));
        Assert.Equal(BsonType.Decimal128, @decimal.Representation);
    }

    [Fact]
    public void An_already_registered_DIFFERENT_serializer_is_refused_and_named()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => PlatformBsonSerializers.Ensure(
            typeof(Guid),
            register: () => throw new BsonSerializationException("There is already a serializer registered for type Guid."),
            lookupExisting: () => new GuidSerializer(GuidRepresentation.CSharpLegacy),
            IsStandardGuid,
            "GuidSerializer(GuidRepresentation.Standard)"));

        Assert.Contains("GuidSerializer(CSharpLegacy)", failure.Message, StringComparison.Ordinal);
        Assert.Contains("GuidSerializer(GuidRepresentation.Standard)", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_already_registered_SAME_serializer_is_a_no_op()
    {
        var failure = Record.Exception(() => PlatformBsonSerializers.Ensure(
            typeof(Guid),
            register: () => throw new BsonSerializationException("There is already a serializer registered for type Guid."),
            lookupExisting: () => new GuidSerializer(GuidRepresentation.Standard),
            IsStandardGuid,
            "GuidSerializer(GuidRepresentation.Standard)"));

        Assert.Null(failure);
    }

    [Fact]
    public void A_first_registration_never_consults_the_existing_serializer()
    {
        var registered = 0;

        PlatformBsonSerializers.Ensure(
            typeof(Guid),
            register: () => registered++,
            lookupExisting: () => throw new InvalidOperationException("must not be looked up when registration succeeds"),
            IsStandardGuid,
            "GuidSerializer(GuidRepresentation.Standard)");

        Assert.Equal(1, registered);
    }

    [Fact]
    public void A_failure_that_is_not_the_registry_refusing_a_duplicate_is_not_swallowed()
    {
        Assert.Throws<ArgumentException>(() => PlatformBsonSerializers.Ensure(
            typeof(Guid),
            register: () => throw new ArgumentException("unrelated"),
            lookupExisting: () => new GuidSerializer(GuidRepresentation.Standard),
            IsStandardGuid,
            "GuidSerializer(GuidRepresentation.Standard)"));
    }
}
