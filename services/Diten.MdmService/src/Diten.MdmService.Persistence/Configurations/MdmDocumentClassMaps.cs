using System.Reflection;
using Diten.MdmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.MdmService.Persistence.Configurations;

/// <summary>
/// WP-MDM-BL576-01 — rollback safety for EVERY document MDM stores, the GP / Brand pattern applied to the rest.
///
/// <para>Before: only Brand / Product (<see cref="BrandProductClassMaps"/>) and the three Global Product lifecycle
/// requests (<see cref="GlobalProductLifecycleClassMaps"/>) ignored extra elements. Any other document (GSKU, LSKU,
/// Finished Good, revision, code reservation, the workflow operations…) that a newer binary had written a field into
/// could not be read by an older one — "Element 'X' does not match any field or property" — so a rollback after a
/// slice that adds a field (S3's stock fields) broke reads of exactly the records that slice had touched.</para>
///
/// <para>What is registered: each persisted root type (<see cref="PersistedRoots"/> — measured from the repositories,
/// kept honest by a guard test) and every MDM Domain class reachable from it through its properties (the embedded
/// bindings, admissions, selections, audit intents…: a field added to one of THOSE breaks a rollback the same way).
/// Each map is exactly what the driver builds on its own (<c>new BsonClassMap(type)</c> + <c>AutoMap()</c>) plus
/// <c>SetIgnoreExtraElements(true)</c> — no member, element name, id, Guid or enum representation changes (a test
/// compares every map with a fresh auto-map). A type someone already mapped (Brand / GP lifecycle) is left as it is.</para>
///
/// <para>Registered once, locked, repeatable; then <see cref="Verify"/>: a type whose map was frozen earlier WITHOUT
/// the flag (a read before registration) stops the start — the BL-569 / GP flow 2 FIX1 item 10 rule. Hosts call
/// <see cref="Register"/> from <c>AddPersistence</c>; test assemblies from their module initializer.</para>
/// </summary>
public static class MdmDocumentClassMaps
{
    private static readonly object Gate = new();
    private static bool _registered;

    private static readonly Lazy<IReadOnlyList<Type>> Roots = new(() =>
    [
        typeof(AuditIntentTemporalMigrationState), typeof(BrandHistoryEntry),
        // The code counter is the code reservation repository's own private document type.
        NestedDocument(typeof(Repositories.CodeReservationRepository), "CanonicalCodeCounter"),
        typeof(CodeReservation), typeof(ExternalReferenceReservation), typeof(FinishedGood),
        typeof(FinishedGoodCreationAttempt), typeof(FinishedGoodIdentityWorkflowOperation),
        typeof(FirstGskuIdentityRetirementOperation), typeof(FirstGskuIdentityWorkflowOperation), typeof(GlobalProduct),
        typeof(GlobalProductCorrectionOperation), typeof(GlobalProductIdentityWorkflowOperation),
        typeof(GlobalProductRetirementRequestOperation), typeof(Gsku), typeof(GskuCorrectionWorkflowOperation),
        typeof(GskuRetirementRequestOperation), typeof(Lsku), typeof(LskuIdentityWorkflowOperation),
        typeof(LskuRetirementRequestOperation), typeof(ProductAbbreviationAllocationLedger),
        typeof(ProductAbbreviationHistoryEntry), typeof(ProductAbbreviationRegisterEntry), typeof(ProductDefinitionRevision),
        typeof(Brand), typeof(Product), typeof(LocalAuditIntent), typeof(LocalAuditIntentReceipt),
        // CT merge reconciliation (BL-576 → takeover, 2026-10-08): FU04 S1's Item. ItemMasterClassMaps maps it first (same
        // AutoMap + flag); the walk then covers what it embeds.
        typeof(Item)
    ]);

    /// <summary>The document types the repositories read and write (collections, raw deserializations, projections) that
    /// ignore extra elements — every stored type except <see cref="StrictByDesignRoots"/>.</summary>
    public static IReadOnlyList<Type> PersistedRoots => Roots.Value;

    /// <summary>
    /// WP-MDM-BL576-01 FIX1 (CT decisions R2, R3) — stored types that STAY STRICT on purpose; a document carrying a field
    /// this binary does not know fails to read instead of being read with the field dropped:
    /// <list type="bullet">
    /// <item><see cref="ProductLegalEntityScopeRolloutState"/> (and its writer leases, fence, abort, snapshot) and
    /// <see cref="ProductLegalEntityScopePolicy"/> (and its periods) — a fail-closed guard: an older binary that ignored a
    /// lease / fence field a newer one added would accept writes the newer fence holds. Today an unknown element is a
    /// coded 503 (PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID): the correct answer.</item>
    /// <item><see cref="LegalEntity"/> — written by whole-document replace (RepositoryBase): a read that succeeded would
    /// make the next replace silently erase the newer fields. Failing the read writes nothing: safer.</item>
    /// </list>
    /// An embedded type shared with a covered document (the audit intents and receipts) stays covered — it is named in
    /// the report.
    /// </summary>
    public static readonly IReadOnlyList<Type> StrictByDesignRoots =
    [
        typeof(ProductLegalEntityScopeRolloutState), typeof(ProductLegalEntityScopePolicy), typeof(LegalEntity)
    ];

    private static readonly Lazy<IReadOnlyList<Type>> Covered = new(() => Reachable(PersistedRoots));
    private static readonly Lazy<IReadOnlyList<Type>> Strict = new(() =>
        Reachable(StrictByDesignRoots).Where(type => !Covered.Value.Contains(type)).ToList());

    /// <summary>Every type this registry covers: the roots and the Domain classes reachable from them, leaves first.</summary>
    public static IReadOnlyList<Type> CoveredTypes => Covered.Value;

    /// <summary>The strict-by-design roots and the Domain classes only they embed — never registered here.</summary>
    public static IReadOnlyList<Type> StrictByDesign => Strict.Value;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            // FIX1 M7: the maps assume production's Guid serializer is already in place (it is registered first, in
            // AddPersistence and in every test initializer); a process that got here without it would write Guids the
            // query side cannot match (BL-280) — said by name instead.
            if (BsonSerializer.LookupSerializer<Guid>() is not GuidSerializer { GuidRepresentation: GuidRepresentation.Standard })
                throw new InvalidOperationException(
                    "BL-576: the Guid serializer is not GuidSerializer(Standard) — register it before the document class maps.");
            // The explicit maps first, ALWAYS: Brand / Product pin their Guid members, the GP lifecycle requests keep
            // theirs — this registry must never be the one that maps those types.
            BrandProductClassMaps.Register();
            GlobalProductLifecycleClassMaps.Register();
            foreach (var type in CoveredTypes)
            {
                if (BsonClassMap.IsClassMapRegistered(type)) continue;
                try
                {
                    BsonClassMap.RegisterClassMap(Create(type));
                }
                catch (ArgumentException) when (BsonClassMap.IsClassMapRegistered(type))
                {
                    // Lost a registration race to an equal map; Verify below still measures it.
                }
            }
            Verify();
            _registered = true;
        }
    }

    private static Type NestedDocument(Type owner, string name) =>
        owner.GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException(
            $"BL-576: the stored document type {owner.Name}.{name} is gone — update MdmDocumentClassMaps' roots.");

    /// <summary>
    /// The map the driver would build on its own, with only the forward-compatibility flag added. Public so a test can
    /// compare it, member by member, with a fresh unregistered auto-map.
    /// </summary>
    public static BsonClassMap Create(Type type)
    {
        var map = new BsonClassMap(type);
        map.AutoMap();
        map.SetIgnoreExtraElements(true);
        return map;
    }

    /// <summary>Public so a test can show it refuses a map that does not ignore extra elements.</summary>
    public static void Verify(IEnumerable<Type>? types = null)
    {
        var unguarded = (types ?? CoveredTypes)
            .Where(type => !BsonClassMap.LookupClassMap(type).IgnoreExtraElements)
            .Select(type => type.Name)
            .ToArray();
        if (unguarded.Length > 0)
            throw new InvalidOperationException(
                "BL-576: the class map(s) of " + string.Join(", ", unguarded) + " do not ignore extra elements — a " +
                "rolled-back binary could not read documents a newer one wrote. A read before registration froze them; " +
                "register the maps first (AddPersistence / the test module initializer).");
    }

    // Depth-first over public instance properties (and element types of collections / nullables), MDM Domain (and
    // Persistence-private document) classes only; abstract bases are not documents of their own (each concrete map carries the flag). Post-order: leaves first.
    private static IReadOnlyList<Type> Reachable(IEnumerable<Type> roots)
    {
        var domain = typeof(EntityBase).Assembly;
        var persistence = typeof(MdmDocumentClassMaps).Assembly;
        var seen = new HashSet<Type>();
        var ordered = new List<Type>();
        void Visit(Type type)
        {
            foreach (var candidate in Expand(type))
            {
                if ((candidate.Assembly != domain && candidate.Assembly != persistence) || !candidate.IsClass || candidate.IsAbstract
                    || candidate.IsGenericTypeDefinition || !seen.Add(candidate))
                    continue;
                foreach (var property in candidate.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Visit(property.PropertyType);
                ordered.Add(candidate);
            }
        }
        foreach (var root in roots) Visit(root);
        return ordered;
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.IsArray) foreach (var inner in Expand(type.GetElementType()!)) yield return inner;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
            foreach (var inner in Expand(argument))
                yield return inner;
    }
}
