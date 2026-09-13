using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.AuthService.Persistence.Serialization;

/// <summary>
/// Registers the service's ONE Guid representation (<see cref="GuidRepresentation.Standard"/>) — process-global, once.
///
/// <para>The driver throws when a Guid serializer has already been registered or merely looked up in this process.
/// A fresh production host never hits that: this runs first, once. A test process that hosts the real Api
/// in-process AFTER something else touched Guid serialization does. Two cases, deliberately told apart:
/// a COMPATIBLE repeat (the resolvable serializer already is <c>GuidSerializer(Standard)</c>) is a no-op; an
/// INCOMPATIBLE one is refused — the host does not start with a representation production does not use, because
/// that fails silently later (a query by id finds nothing, BL-280 shape). Test isolation, not a softer rule, keeps
/// the refusal out of the test suite: <c>Testing/AuthTestSerializers.cs</c> registers Standard at assembly load,
/// before the first test case, so every later call here is the compatible repeat.</para>
/// </summary>
public static class GuidSerializerRegistration
{
    public static void EnsureStandard() =>
        EnsureStandard(
            () => BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)),
            () => BsonSerializer.LookupSerializer(typeof(Guid)));

    /// <summary>The decision, with its two process-global effects injected so a unit test can drive both branches.</summary>
    public static void EnsureStandard(Action register, Func<IBsonSerializer> lookupExisting)
    {
        try
        {
            register();
        }
        catch (BsonSerializationException)
        {
            var existing = lookupExisting();
            if (!IsCompatible(existing))
            {
                throw new InvalidOperationException(
                    "A Guid serializer is already registered in this process with an incompatible shape "
                    + $"({existing.GetType().FullName}); Diten.AuthService requires GuidSerializer(GuidRepresentation.Standard) "
                    + "and refuses to start with anything else. In a test process, register the Standard serializer "
                    + "before anything touches Guid serialization (see Testing/AuthTestSerializers.cs).");
            }
            // Compatible repeat: the existing Standard registration stands; nothing to do.
        }
    }

    public static bool IsCompatible(IBsonSerializer existingGuidSerializer) =>
        existingGuidSerializer is GuidSerializer { GuidRepresentation: GuidRepresentation.Standard };
}
