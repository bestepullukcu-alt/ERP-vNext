using MongoDB.Bson.Serialization.Conventions;

namespace Diten.Platform.Infrastructure.Persistence;

/// <summary>
/// BL-384 — the process-wide BSON conventions Platform reads and writes its documents with.
///
/// <para><b>The failure this prevents.</b> On 2026-09-13 an older Platform build read <c>meeting_record_links</c>
/// documents written by a newer build and threw <c>FormatException: Element 'IdempotencyKey' does not match any
/// field</c>; the task source and the meeting list answered 500. The driver's default is to REJECT an element the
/// class map does not know, so every field a release adds would crash the release before it — exactly the build a
/// rollback puts back in front of production data.</para>
///
/// <para><b>Why a convention and not an attribute per entity.</b> An attribute has to be remembered on every new
/// entity and every embedded type; the one that is forgotten is the one that crashes the rollback. A convention
/// covers every class map built after it, including embedded documents.</para>
///
/// <para><b>An explicit opt-out still wins.</b> A type that must reject unknown elements declares
/// <c>[BsonIgnoreExtraElements(false)]</c>: the driver applies attribute conventions after registered packs, so
/// the attribute overrides this default. Measured by
/// <c>UnknownFieldToleranceMongoTests.An_explicit_attribute_still_opts_a_type_out</c>.</para>
///
/// <para>⚠ <b>ORDER MATTERS.</b> A class map is built once, on the first serialization of its type, and never
/// re-reads the registry afterwards. <see cref="Register"/> must therefore run before any Platform document is
/// serialized: it is the first BSON registration in <c>DependencyInjection.AddInfrastructure</c>, and the test
/// assembly calls this same method from its <c>[ModuleInitializer]</c> rather than keeping a copy of it.</para>
/// </summary>
public static class PlatformBsonConventions
{
    /// <summary>The name the pack is registered under in <see cref="ConventionRegistry"/>.</summary>
    public const string IgnoreExtraElementsPackName = "Diten.Platform.IgnoreExtraElements";

    private static int _registered;

    /// <summary>
    /// Registers the conventions once per process. Safe to call again: <see cref="ConventionRegistry"/> does not
    /// de-duplicate by name, so a second registration would otherwise add a second copy of the pack.
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        ConventionRegistry.Register(
            IgnoreExtraElementsPackName,
            new ConventionPack { new IgnoreExtraElementsConvention(true) },
            _ => true);
    }
}
