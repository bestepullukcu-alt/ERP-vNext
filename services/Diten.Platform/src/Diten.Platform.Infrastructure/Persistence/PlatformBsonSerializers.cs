using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.Platform.Infrastructure.Persistence;

/// <summary>
/// The two process-global BSON serializers Platform stores its documents with: Guids as
/// <see cref="GuidRepresentation.Standard"/>, decimals as <see cref="BsonType.Decimal128"/>.
///
/// <para><b>Why this is not a bare <c>RegisterSerializer</c>.</b> The driver's registry is process-global and
/// <c>RegisterSerializer</c> throws once a serializer for the type has been registered or merely looked up. A
/// production host never hits that: this runs once, first. A process that composes <c>AddInfrastructure</c> a
/// second time does — measured: both <c>PlatformContainerValidationTests</c> cases compose it in one test process
/// and the second died with "There is already a serializer registered for type Guid", red about the registry
/// rather than about the container it exists to validate.</para>
///
/// <para><b>Repeatable, not forgiving.</b> A repeat is accepted only when the serializer the process already
/// resolves IS the required one. Anything else is refused with the type named: a host that started with another
/// Guid or decimal shape would not fail here, it would fail later and silently — a query by id that finds nothing,
/// a money field written as a string (the BL-280 shape). "It is already registered, carry on" is exactly the
/// answer this must never give.</para>
/// </summary>
public static class PlatformBsonSerializers
{
    public static void EnsureRegistered()
    {
        Ensure(
            new GuidSerializer(GuidRepresentation.Standard),
            existing => existing is GuidSerializer { GuidRepresentation: GuidRepresentation.Standard },
            "GuidSerializer(GuidRepresentation.Standard)");
        Ensure(
            new DecimalSerializer(BsonType.Decimal128),
            existing => existing is DecimalSerializer { Representation: BsonType.Decimal128 },
            "DecimalSerializer(BsonType.Decimal128)");
    }

    private static void Ensure<T>(
        IBsonSerializer<T> required,
        Func<IBsonSerializer, bool> isRequiredShape,
        string requiredDescription) =>
        Ensure(
            typeof(T),
            () => BsonSerializer.RegisterSerializer(required),
            () => BsonSerializer.LookupSerializer(typeof(T)),
            isRequiredShape,
            requiredDescription);

    /// <summary>The decision, with its two process-global effects injected so a test can drive both branches.</summary>
    public static void Ensure(
        Type valueType,
        Action register,
        Func<IBsonSerializer> lookupExisting,
        Func<IBsonSerializer, bool> isRequiredShape,
        string requiredDescription)
    {
        try
        {
            register();
        }
        catch (BsonSerializationException)
        {
            var existing = lookupExisting();
            if (!isRequiredShape(existing))
            {
                throw new InvalidOperationException(
                    $"A different serializer is already registered for {valueType.Name} in this process "
                    + $"({Describe(existing)}); Diten.Platform requires {requiredDescription} and refuses to start "
                    + "with anything else. In a test process, register the production serializers before anything "
                    + "serializes a document (see PlatformTestSerializers).");
            }

            // The same serializer is already in place: the earlier registration stands, nothing to do.
        }
    }

    private static string Describe(IBsonSerializer serializer) => serializer switch
    {
        GuidSerializer guid => $"GuidSerializer({guid.GuidRepresentation})",
        DecimalSerializer @decimal => $"DecimalSerializer({@decimal.Representation})",
        _ => serializer.GetType().FullName ?? serializer.GetType().Name
    };
}
