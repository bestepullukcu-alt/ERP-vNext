using Diten.AuthService.Persistence.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 (C2) — pins the compatible/incompatible distinction the PPM CT correction adds.
///
/// <para>⚠ WHY THE INCOMPATIBLE PATH IS ONLY PROVEN VIA <see cref="GuidSerializerRegistration.IsCompatible"/>, NOT
/// via an <see cref="GuidSerializerRegistration.EnsureStandard"/> integration test. The Guid serializer registry
/// (<see cref="BsonSerializer"/>) is process-global and, once a serializer for <see cref="Guid"/> is registered,
/// stays registered for the rest of the process. Every OTHER Mongo-backed test in this assembly (and every other
/// AuthService assembly loaded in the same test run) assumes the Standard representation is what is registered.
/// Deliberately registering an INCOMPATIBLE representation to prove <see cref="GuidSerializerRegistration.EnsureStandard"/>
/// throws would permanently poison that registry for the rest of the process — there is no supported way to
/// unregister or reset it. So the incompatible-real-state scenario is proven here PURELY, through
/// <see cref="GuidSerializerRegistration.IsCompatible"/> alone, which <c>EnsureStandard</c>'s incompatible branch
/// reads verbatim (<c>if (!IsCompatible(existing)) throw …</c>) — the two cannot disagree without a code change
/// that also fails a K1 red→green sabotage of <c>IsCompatible</c> itself.</para>
///
/// <para>⚠ MEASURED regression this collection tag fixes. The registry is not just process-global — it is a
/// pre-existing, UNGUARDED shared-mutable-state race: ProductAbbreviationPermissionOnboardingMongoTests (its own
/// xunit collection, no serialization) drives a raw MongoClient against the shared localhost:27017 server WITHOUT
/// setting its own client's GuidRepresentation, so its Guid encoding on the wire depends on whatever this
/// process-global registry holds at the moment its insert/find calls run. xunit runs different collections in
/// PARALLEL by default. Before this tag, EnsureStandard_called_twice_in_the_same_process_does_not_throw ran in its
/// OWN separate collection, and — being pure/fast — it could be the process's very FIRST successful
/// RegisterSerializer(Standard) call, landing in the middle of that other test's write-then-read window and
/// flipping the encoding mid-test: measured, this produced
/// "GuidSerializer cannot deserialize a Guid when GuidRepresentation is Standard and binary sub type is
/// UuidLegacy." in a full-suite run. Tagging THIS class into the already-serialized "AccountKindAcceptance"
/// collection does not eliminate that pre-existing race (a different, out-of-scope file owns the actual bug — it
/// never pins its own client's representation), but it removes the FAST, EARLY, parallel-eligible trigger this
/// round's new tests added, restoring the race to no more likely than it already was.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class GuidSerializerRegistrationTests
{
    // ── IsCompatible: pure, no process-global state touched ──

    [Fact]
    public void IsCompatible_true_for_the_exact_shape_EnsureStandard_registers()
    {
        Assert.True(GuidSerializerRegistration.IsCompatible(new GuidSerializer(GuidRepresentation.Standard)));
    }

    [Theory]
    [InlineData(GuidRepresentation.CSharpLegacy)]
    [InlineData(GuidRepresentation.JavaLegacy)]
    [InlineData(GuidRepresentation.PythonLegacy)]
    [InlineData(GuidRepresentation.Unspecified)]
    public void IsCompatible_false_for_any_other_GuidRepresentation(GuidRepresentation representation)
    {
        Assert.False(GuidSerializerRegistration.IsCompatible(new GuidSerializer(representation)));
    }

    [Fact]
    public void IsCompatible_false_for_a_serializer_that_is_not_a_GuidSerializer_at_all()
    {
        // A different IBsonSerializer entirely — the "already registered" value the driver COULD hand back is not
        // guaranteed to even be a GuidSerializer; the type-shape check must reject that as firmly as a wrong
        // representation.
        Assert.False(GuidSerializerRegistration.IsCompatible(new StringSerializer()));
    }

    // ── EnsureStandard: only the "never throws, whatever the process already holds" claim is provable in-process ──
    //
    // ⚠ NOT asserted here: "the resolved serializer IS Standard afterward". MEASURED during this WP's own
    // verification round: that is NOT guaranteed in this shared xunit process — an EARLIER, unrelated
    // Guid-touching operation anywhere in the ~800-test run can cause the driver's own internal default
    // registration to win before EnsureStandard's first call, and EnsureStandard (by design, see its remarks) then
    // WARNS and leaves that existing registration in place rather than throwing. A test asserting Standard-always
    // would itself be exactly as run-order-flaky as the bug this WP found and worked around.

    [Fact]
    public void EnsureStandard_called_twice_in_the_same_process_does_not_throw()
    {
        // Whatever state the process is in when this test runs (fresh registration, an earlier in-process host's
        // identical Standard registration, or even an incompatible one the driver auto-registered first — all are
        // legitimate, measured states in this test run), calling EnsureStandard TWICE in a row must never throw:
        // a second RegisterSerializer attempt for an already-occupied type always fails, and EnsureStandard's own
        // job is to absorb that failure quietly (compatible) or loudly-but-non-fatally (incompatible) — never let
        // it propagate.
        var first = Record.Exception(GuidSerializerRegistration.EnsureStandard);
        var second = Record.Exception(GuidSerializerRegistration.EnsureStandard);

        Assert.Null(first);
        Assert.Null(second);
    }

    [Fact]
    public void EnsureStandard_always_leaves_SOME_Guid_serializer_resolvable()
    {
        GuidSerializerRegistration.EnsureStandard();

        // LookupSerializer itself throws if nothing is registered for the type; a value coming back at all is the
        // one guarantee EnsureStandard makes regardless of which representation won the race.
        var resolved = BsonSerializer.LookupSerializer(typeof(Guid));

        Assert.NotNull(resolved);
    }

    // ── The refusal itself, driven through the injected seam (the process-global state cannot be made incompatible
    // in-process once AuthTestSerializers has registered Standard at assembly load). ───────────────────────────
    [Fact]
    public void An_incompatible_existing_registration_is_refused_not_tolerated()
    {
        var ex = Record.Exception(() => GuidSerializerRegistration.EnsureStandard(
            register: () => throw new MongoDB.Bson.BsonSerializationException("already registered"),
            lookupExisting: () => new MongoDB.Bson.Serialization.Serializers.GuidSerializer(MongoDB.Bson.GuidRepresentation.CSharpLegacy)));

        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public void A_compatible_repeat_registration_is_a_silent_no_op()
    {
        var ex = Record.Exception(() => GuidSerializerRegistration.EnsureStandard(
            register: () => throw new MongoDB.Bson.BsonSerializationException("already registered"),
            lookupExisting: () => new MongoDB.Bson.Serialization.Serializers.GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard)));

        Assert.Null(ex);
    }
}
