using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence;

/// <summary>
/// CRM persistence wiring (MOD-0149). Registers Mongo client/database, repositories and indexes.
/// Guids are serialized via the globally-registered standard (binary subtype-4) serializer; entities
/// AutoMap on first use. Reference-data values are NOT stored here (SoR = MOD-0048 / PSS-012).
/// </summary>
public static class DependencyInjection
{
    private static bool _guidSerializerRegistered;
    private static bool _classMapsRegistered;

    // Registration mutates process-wide BSON state. Under the parallel test runner two threads could both see the
    // flag as false and race through the same registrations, which surfaces as an intermittent
    // "class map already registered" failure rather than as anything real. One lock removes the race entirely.
    private static readonly object SerializationRegistrationLock = new();

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterGuidSerializer();
        RegisterClassMaps();

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing.");

        var client = new MongoClient(MongoClientSettings.FromConnectionString(connectionString));

        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountExternalReferenceRepository, AccountExternalReferenceRepository>();
        services.AddScoped<IAccountAttributeValueRepository, AccountAttributeValueRepository>();
        services.AddScoped<IAccountCodeSequenceRepository, AccountCodeSequenceRepository>();

        // MOD-0150 FU01 — Contact Foundation
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IContactExternalReferenceRepository, ContactExternalReferenceRepository>();

        // MOD-0150 FU03 — Account↔Contact links
        services.AddScoped<IAccountContactLinkRepository, AccountContactLinkRepository>();

        // MOD-0150 FU04 — Account↔Account relationships
        services.AddScoped<IAccountRelationshipRepository, AccountRelationshipRepository>();

        // MOD-0150 FU07 — AccountContactLink-scoped availability master + date-specific exceptions. Neither
        // repository exposes a delete: closing a row is a status update (inactive/archived).
        services.AddScoped<IContactAvailabilityRepository, ContactAvailabilityRepository>();
        services.AddScoped<IContactAvailabilityExceptionRepository, ContactAvailabilityExceptionRepository>();

        // MOD-0151 FU01 — Territory model + node aggregates
        services.AddScoped<ITerritoryModelRepository, TerritoryModelRepository>();
        services.AddScoped<ITerritoryNodeRepository, TerritoryNodeRepository>();

        // MOD-0151 FU03 — assignment rules + the READ-ONLY account seam the preview matcher consumes.
        services.AddScoped<ITerritoryAssignmentRuleRepository, TerritoryAssignmentRuleRepository>();
        services.AddScoped<ITerritoryAccountReader, TerritoryAccountReader>();

        // MOD-0151 FU04 — resource (person) assignments. No employee master here; only a PersonRef seam.
        services.AddScoped<ITerritoryResourceAssignmentRepository, TerritoryResourceAssignmentRepository>();
        services.AddScoped<ITerritoryActivationUnitOfWork, TerritoryActivationUnitOfWork>();
        services.AddScoped<ITerritoryDraftCloneUnitOfWork, TerritoryDraftCloneUnitOfWork>();
        services.AddScoped<IAccountTerritoryAssignmentRepository, AccountTerritoryAssignmentRepository>();

        // MOD-0151 FU04B — immutable activation plan baseline (read-only repository; the only write is the
        // activation unit of work above).
        services.AddScoped<ITerritoryResourceAssignmentPlanSnapshotRepository,
            TerritoryResourceAssignmentPlanSnapshotRepository>();

        // MOD-0151 FU08 — append-only import run history (insert + read only; no update/delete path exists).
        services.AddScoped<ITerritoryImportRunRepository, TerritoryImportRunRepository>();

        // MOD-0165 FU03 — Visit Frequency / Call-Cycle Policy master + read-only resolve seam. No delete method:
        // closing a policy is a status update (inactive/archived).
        services.AddScoped<IVisitFrequencyPolicyRepository, VisitFrequencyPolicyRepository>();

        // MOD-0164 FU02 — consent + preference masters and the read-only evaluation seam. Neither repository exposes a
        // delete: closing a record is the soft archive lifecycle, so consent history stays readable forever.
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();
        services.AddScoped<IPreferenceRecordRepository, PreferenceRecordRepository>();

        // MOD-0165 FU04 — campaign + campaign target stores. Neither exposes a delete: closing either is the soft
        // archive lifecycle, and a target snapshot is additive (it can insert or replace, never remove).
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        // MOD-0165 FU08 - the campaign write path's cycle-binding guard. Scoped like every other write-path component;
        // it holds only the read-only ICyclePeriodReader seam (no repository, no HttpClient).
        services.AddScoped<Application.Features.Campaign.CampaignCycleBindingGuard>();
        // MOD-0165 FU09 - the campaign scope write gate. Scoped like every other write-path component; it holds the
        // shared read-only reference/MDM seams and no repository.
        services.AddScoped<Application.Features.Campaign.Services.CampaignScopeWriteValidator>();
        // MOD-0165 FU10 - targeting gate + code sequence + the read-only segment window.
        services.AddScoped<Application.Features.Campaign.Services.CampaignSegmentValidator>();
        services.AddScoped<
            Application.Features.Campaign.ICampaignCodeGenerator,
            Application.Features.Campaign.CampaignCodeGenerator>();
        services.AddScoped<ICampaignCodeSequenceRepository, CampaignCodeSequenceRepository>();
        services.AddScoped<
            Application.Features.Campaign.Read.ICampaignSegmentCatalog,
            Repositories.CampaignSegmentCatalog>();
        services.AddScoped<ICampaignTargetRepository, CampaignTargetRepository>();

        // MOD-0162 FU02 — Knowledge content + subject/topic/audience-profile taxonomy masters. None exposes a delete:
        // closing any of them is the soft archive lifecycle, so classification/content history stays readable.
        services.AddScoped<IKnowledgeContentRepository, KnowledgeContentRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITopicRepository, TopicRepository>();
        services.AddScoped<IAudienceProfileRepository, AudienceProfileRepository>();
        // Read-only content-linkage seam a future Campaign/MOD-0155 consumer reads; makes no decision and mutates nothing.
        services.AddScoped<
            Application.Features.Knowledge.Content.IKnowledgeContentLinkageReader,
            Application.Features.Knowledge.Content.KnowledgeContentLinkageReader>();

        // MOD-0162 FU03 — Concept graph (type / node / relationship / chain-template / content-link). Configuration
        // surface only: no traversal / resolution / recommendation engine. None exposes a delete (soft archive).
        services.AddScoped<IConceptTypeRepository, ConceptTypeRepository>();
        services.AddScoped<IConceptNodeRepository, ConceptNodeRepository>();
        services.AddScoped<IConceptRelationshipRepository, ConceptRelationshipRepository>();
        services.AddScoped<IConceptChainTemplateRepository, ConceptChainTemplateRepository>();
        services.AddScoped<IKnowledgeContentConceptLinkRepository, KnowledgeContentConceptLinkRepository>();

        // MOD-0162 FU04 — KnowledgePath master (steps embedded, D2 → one collection, one repository). No delete method
        // (soft archive). The read-only consumption seam a future MOD-0155/MOD-0309 consumer reads makes no decision.
        services.AddScoped<IKnowledgePathRepository, KnowledgePathRepository>();
        services.AddScoped<
            Application.Features.Knowledge.Path.IKnowledgePathReader,
            Application.Features.Knowledge.Path.KnowledgePathReader>();

        // MOD-0162 FU05 — ContentEngagementJourney master (stages embedded, S2 → one collection, one repository). No
        // delete method (soft archive). The read-only consumption seam a future MOD-0155/MOD-0309 consumer reads makes
        // no decision: no advancement, no current stage, no recommendation.
        services.AddScoped<IContentEngagementJourneyRepository, ContentEngagementJourneyRepository>();
        services.AddScoped<
            Application.Features.Knowledge.ContentEngagementJourney.IContentEngagementJourneyReader,
            Application.Features.Knowledge.ContentEngagementJourney.ContentEngagementJourneyReader>();

        // MOD-0167 FU02 - Segment (criteria embedded, D2) + TargetCustomer (manual membership only) masters. Neither
        // exposes a delete: closing either is the soft archive lifecycle, so a past selection stays explainable.
        services.AddScoped<ISegmentRepository, SegmentRepository>();
        services.AddScoped<ITargetCustomerRepository, TargetCustomerRepository>();

        // The bounded, two-phase resolution stack. Every reader below is BULK by construction: one read per source for
        // the whole candidate set, never one per candidate. Membership itself is never persisted (D3), so there is no
        // membership repository here and there never will be one in this FU.
        services.AddScoped<
            Application.Features.Segmentation.Resolution.ISegmentCandidateSource, SegmentCandidateSource>();
        services.AddScoped<
            Application.Features.Segmentation.Resolution.ISegmentConsentBulkReader, SegmentConsentBulkReader>();
        services.AddScoped<
            Application.Features.Segmentation.Resolution.ISegmentTerritoryCoverageReader,
            Application.Features.Segmentation.Resolution.SegmentTerritoryCoverageReader>();
        // READ-ONLY consumption of the MOD-0162 FU03 concept graph. Scoped so its list read is memoised per request:
        // one node read and one relationship read per resolution, no matter how many candidates.
        services.AddScoped<
            Application.Features.Segmentation.Resolution.ISegmentConceptAffinityReader,
            Application.Features.Segmentation.Resolution.ConceptAffinitySourceReader>();
        services.AddScoped<
            Application.Features.Segmentation.Resolution.ISegmentAttributeSourceReader,
            Application.Features.Segmentation.Resolution.SegmentAttributeSourceReader>();
        services.AddScoped<Application.Features.Segmentation.Resolution.SegmentMembershipResolver>();
        // The read-only consumption seam MOD-0167-FU01 and a future MOD-0165 snapshot read. It reports and never
        // writes: no CampaignTarget, no VisitFrequencyPolicy, nothing.
        services.AddScoped<
            Application.Features.Segmentation.Resolution.ISegmentMembershipReader,
            Application.Features.Segmentation.Resolution.SegmentMembershipReader>();

        // MOD-0167 FU04 - StrategyTemplate master (all four binding lists embedded -> one collection, one optimistic
        // token). No delete: closing a play is the soft archive lifecycle. The binding validator READS the segment,
        // policy and content masters and writes to none of them, and the consumption seam a future MOD-0155 reads
        // reports bindings only: no MicroTarget, no VisitFrequencyPolicy, no CampaignTarget is ever produced here.
        services.AddScoped<IStrategyTemplateRepository, StrategyTemplateRepository>();
        services.AddScoped<Application.Features.StrategyTemplate.Binding.StrategyTemplateBindingValidator>();
        services.AddScoped<
            Application.Features.StrategyTemplate.Binding.IStrategyTemplateReader,
            Application.Features.StrategyTemplate.Binding.StrategyTemplateReader>();

        // MOD-0165 FU06 - CyclePeriod master (one collection). No delete: ending a period is the closed lifecycle.
        // The read seam answers "which period is in force?" and writes NOTHING - it holds no HttpClient (no self-call)
        // and no MicroTarget / Campaign / VisitFrequencyPolicy / StrategyTemplate dependency, so resolving a period can
        // never become a doorway into another module's aggregate.
        services.AddScoped<ICyclePeriodRepository, CyclePeriodRepository>();
        services.AddScoped<
            Application.Features.CyclePeriod.Read.ICyclePeriodReader,
            Application.Features.CyclePeriod.Read.CyclePeriodReader>();
        // MOD-0165 FU07 - the write path's scope gate: the single-reference invariant, the governed country and
        // business-unit vocabularies, and the fail-closed MDM check, in one place so create and draft-edit cannot
        // drift. Everything it does happens BEFORE any insert or replace.
        services.AddScoped<Application.Features.CyclePeriod.Services.CyclePeriodScopeWriteValidator>();

        // MOD-0155 FU06 - CycleCapacity: the visit-capacity model of ONE cycle period (one collection, month rows
        // EMBEDDED so the aggregate is a single document). No delete: retiring a capacity is the soft archive.
        // The write gate and the country resolver are scoped like every other write-path component; neither holds a
        // CyclePeriod repository - the period is reached only through the read-only ICyclePeriodReader seam, which is
        // what makes "FU06 never writes to CyclePeriod" structural rather than a convention.
        services.AddScoped<ICycleCapacityRepository, CycleCapacityRepository>();
        services.AddScoped<
            Application.Features.CycleCapacity.Services.ICycleCapacityCountryResolver,
            Application.Features.CycleCapacity.Services.CycleCapacityCountryResolver>();
        services.AddScoped<Application.Features.CycleCapacity.Services.CycleCapacityWriteValidator>();
        // The shared "resolve the months, then do the arithmetic" component. ONE registration for BOTH the saved
        // capacity's /calculation and the form's live /calculation-preview, so the fail-closed calendar policy cannot
        // drift between the number an author sees while typing and the number the saved record reports.
        // It holds no repository: a capacity is handed to it, never fetched or stored by it - which is what lets the
        // preview path pass a TRANSIENT capacity that has no row behind it.
        services.AddScoped<Application.Features.CycleCapacity.Services.CycleCapacityEstimator>();

        // MOD-0155 FU01 - PlannedVisit: the field team's planning atom (one collection). No delete: a plan is
        // cancelled/archived so its history stays readable. Single-document writes only, guarded by the Version token.
        services.AddScoped<IPlannedVisitRepository, PlannedVisitRepository>();

        // MOD-0155 FU05 - PlanningSession staging store + the atomic apply/re-plan unit of work. The unit of work spans
        // planning_sessions + planned_visits in one all-or-nothing operation (transaction on a replica set, compensated
        // sequential writes on dev standalone Mongo), so a half-applied plan can never survive (D-APPLY-ATOMICITY = C).
        services.AddScoped<IPlanningSessionRepository, PlanningSessionRepository>();
        services.AddScoped<IPlanningSessionApplyUnitOfWork, PlanningSessionApplyUnitOfWork>();

        // MOD-0155 FU02 - VisitReport: the immutable record of an EXECUTED visit (one collection). No delete: a report is
        // a compliance record, corrections are append-only amendments. Single-document writes only, guarded by the
        // Version token - the report submit/amend touch only this aggregate, and the D-EXECUTION-STATUS = A plan
        // reflection is a documented no-op (FU01 has no "executed" transition, F-EXECUTED-MARKER), so there is no second
        // aggregate to keep consistent and no multi-document transaction is needed.
        services.AddScoped<IVisitReportRepository, VisitReportRepository>();

        TryEnsureIndexes(client.GetDatabase(databaseName));

        return services;
    }

    /// <summary>Test-only hook: registers the Guid serializer + class maps without a Mongo connection, so a unit test
    /// can assert the FU04 embedded types are mapped (else their Guid members would serialize as binary).</summary>
    public static void EnsureClassMapsForTests()
    {
        RegisterGuidSerializer();
        RegisterClassMaps();
    }

    private static void RegisterGuidSerializer()
    {
        lock (SerializationRegistrationLock)
        {
            if (_guidSerializerRegistered)
            {
                return;
            }

            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                // Another service/test host may have already registered the serializer in-process.
            }

            _guidSerializerRegistered = true;
        }
    }

    /// <summary>
    /// Stores Guids as BSON strings (same convention as MdmService/HcmService). Without this the driver writes
    /// Guids in the legacy binary sub type (UuidLegacy/3) while the registered standard GuidSerializer reads
    /// sub type 4, producing: "GuidSerializer cannot deserialize a Guid when GuidRepresentation is Standard and
    /// binary sub type is UuidLegacy". Id/TenantId live on <see cref="EntityBase"/>, so the id member is mapped on
    /// the BASE class map (mapping an inherited member from a derived map throws); derived maps AutoMap and inherit it.
    /// </summary>
    private static void RegisterClassMaps()
    {
        lock (SerializationRegistrationLock)
        {
            RegisterClassMapsCore();
        }
    }

    private static void RegisterClassMapsCore()
    {
        if (_classMapsRegistered)
        {
            return;
        }

        var stringGuid = new GuidSerializer(BsonType.String);

        if (!BsonClassMap.IsClassMapRegistered(typeof(EntityBase)))
        {
            BsonClassMap.RegisterClassMap<EntityBase>(map =>
            {
                map.AutoMap();
                map.MapIdMember(e => e.Id).SetSerializer(stringGuid);
                map.GetMemberMap(e => e.TenantId).SetSerializer(stringGuid);
            });
        }

        Map<Account>(map => map.GetMemberMap(a => a.ParentAccountId)
            .SetSerializer(new NullableSerializer<Guid>(stringGuid)));
        Map<AccountExternalReference>(map => map.GetMemberMap(r => r.AccountId).SetSerializer(stringGuid));
        Map<AccountAttributeValue>(map => map.GetMemberMap(v => v.AccountId).SetSerializer(stringGuid));
        Map<AccountCodeSequence>(_ => { });

        // MOD-0150 FU01 — Contact aggregates (Guid-as-string, same convention).
        Map<Contact>(_ => { });
        Map<ContactExternalReference>(map => map.GetMemberMap(r => r.ContactId).SetSerializer(stringGuid));

        // MOD-0150 FU03 — AccountContactLink (Account/Contact ids as strings; nullable reports-to id as string).
        Map<AccountContactLink>(map =>
        {
            map.GetMemberMap(l => l.AccountId).SetSerializer(stringGuid);
            map.GetMemberMap(l => l.ContactId).SetSerializer(stringGuid);
            map.GetMemberMap(l => l.ReportsToContactId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });

        // MOD-0150 FU04 — AccountRelationship (Source/Target ids as strings).
        Map<AccountRelationship>(map =>
        {
            map.GetMemberMap(r => r.SourceAccountId).SetSerializer(stringGuid);
            map.GetMemberMap(r => r.TargetAccountId).SetSerializer(stringGuid);
        });

        // MOD-0150 FU07 — availability + exceptions. Every Guid FK takes the string-Guid convention: without it the
        // link/contact/account filters serialize a string while the stored value is binary, and the lookup silently
        // returns nothing (the failure AccountTerritoryAssignment already hit). VisitPreference is an owned value
        // object with no Guid member, so it only needs a class map so the driver does not treat it as anonymous.
        Map<ContactAvailability>(map =>
        {
            map.GetMemberMap(a => a.AccountContactLinkId).SetSerializer(stringGuid);
            map.GetMemberMap(a => a.ContactId).SetSerializer(stringGuid);
            map.GetMemberMap(a => a.AccountId).SetSerializer(stringGuid);
        });
        Map<ContactAvailabilityException>(map =>
        {
            map.GetMemberMap(e => e.AccountContactLinkId).SetSerializer(stringGuid);
            map.GetMemberMap(e => e.ContactId).SetSerializer(stringGuid);
            map.GetMemberMap(e => e.AccountId).SetSerializer(stringGuid);
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(VisitPreference)))
        {
            BsonClassMap.RegisterClassMap<VisitPreference>(map =>
            {
                map.AutoMap();
                map.UnmapMember(p => p.HasPreferredWindow);
                map.UnmapMember(p => p.HasAvoidWindow);
            });
        }

        // MOD-0151 FU01 — Territory aggregates (Guid-as-string, same convention). MicroZoneProfile.AnchorAccountId
        // is an owned value-object field; the base string-Guid serializer is applied to the nullable id member.
        Map<TerritoryModel>(map => map.GetMemberMap(m => m.BasedOnModelId).SetSerializer(new NullableSerializer<Guid>(stringGuid)));
        Map<TerritoryNode>(map =>
        {
            map.GetMemberMap(n => n.ModelId).SetSerializer(stringGuid);
            map.GetMemberMap(n => n.ParentTerritoryId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(MicroZoneProfile)))
        {
            BsonClassMap.RegisterClassMap<MicroZoneProfile>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(p => p.AnchorAccountId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }

        // MOD-0151 FU03 — assignment rule (model/territory ids as strings; criteria is an owned value object whose
        // account-id lists use the same string-Guid convention).
        Map<TerritoryAssignmentRule>(map =>
        {
            map.GetMemberMap(r => r.ModelId).SetSerializer(stringGuid);
            map.GetMemberMap(r => r.TerritoryId).SetSerializer(stringGuid);
        });
        // MOD-0151 FU04 — resource assignment (model/territory ids as strings; ResourceRef is an owned value object
        // holding an EXTERNAL id as a plain string, deliberately not a Guid: the owning master may be Person, User
        // or HCM Employee.
        Map<TerritoryResourceAssignment>(map =>
        {
            map.GetMemberMap(a => a.ModelId).SetSerializer(stringGuid);
            map.GetMemberMap(a => a.TerritoryId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        Map<TerritoryResourceRef>(_ => { });
        Map<TerritoryPositionRef>(_ => { });

        // MOD-0151 FU05 — account-territory assignment (all FK ids as strings, same convention as every other
        // territory/account aggregate). WITHOUT this map the aggregate was missed entirely, so its Guids fell
        // through to the global Standard serializer (binary sub-type 4). The repo's own model/account filters
        // then serialize the id as a string-vs-binary MISMATCH and never match the docs they just wrote — the
        // coverage list comes back empty (the "Assigned To" column stayed "—"). Keeping ids as strings also
        // matches how TenantId (EntityBase) is already stored.
        Map<AccountTerritoryAssignment>(map =>
        {
            map.GetMemberMap(a => a.AccountId).SetSerializer(stringGuid);
            map.GetMemberMap(a => a.TerritoryModelId).SetSerializer(stringGuid);
            map.GetMemberMap(a => a.TerritoryNodeId).SetSerializer(stringGuid);
            map.GetMemberMap(a => a.AppliedFromPreviewRunId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(a => a.AppliedRuleId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(a => a.MigratedFromAssignmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(a => a.MigratedFromModelId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });

        // MOD-0151 FU04B — plan baseline snapshot. TerritoryModelId is a Guid FK, so it MUST be mapped to the
        // string-Guid convention like every other territory aggregate; leaving it to the global Standard serializer
        // stores it as binary while the repo filter serializes a string, and the lookup silently returns nothing
        // (the failure mode AccountTerritoryAssignment already hit). Snapshot LINES carry no Guid FK except
        // SourceAssignmentId, which is mapped on the line class map below.
        Map<TerritoryResourceAssignmentPlanSnapshot>(map =>
            map.GetMemberMap(s => s.TerritoryModelId).SetSerializer(stringGuid));
        if (!BsonClassMap.IsClassMapRegistered(typeof(TerritoryResourceAssignmentPlanSnapshotLine)))
        {
            BsonClassMap.RegisterClassMap<TerritoryResourceAssignmentPlanSnapshotLine>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(l => l.TerritoryNodeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(l => l.SourceAssignmentId).SetSerializer(stringGuid);
            });
        }

        // MOD-0151 FU08 — import run history. TerritoryModelId is a Guid FK, so it takes the same string-Guid
        // convention as every other territory aggregate; leaving it to the global Standard serializer would store it
        // as binary while the repository filter serializes a string, and the history lookup would silently return
        // nothing (the exact failure AccountTerritoryAssignment already hit).
        Map<TerritoryImportRun>(map => map.GetMemberMap(r => r.TerritoryModelId).SetSerializer(stringGuid));
        Map<TerritoryImportRunResult>(_ => { });
        Map<TerritoryImportRunSheetCount>(_ => { });

        // MOD-0165 FU03 — VisitFrequencyPolicy. Every Guid FK takes the string-Guid convention (same as every other
        // CRM aggregate): without it the target/segment/campaign filters serialize a string while the stored value is
        // binary, and the lookup silently returns nothing (the failure AccountTerritoryAssignment already hit).
        Map<VisitFrequencyPolicy>(map =>
        {
            map.GetMemberMap(p => p.TargetId).SetSerializer(stringGuid);
            map.GetMemberMap(p => p.TerritoryNodeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.CampaignId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.SegmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.BrandId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.ProductId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.CycleId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.CyclePeriodId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });

        // MOD-0164 FU02 — ConsentRecord / PreferenceRecord. SubjectId and ScopeId are Guid FKs, so they take the
        // string-Guid convention like every other CRM aggregate: without it the evaluation filter serializes a string
        // while the stored value is binary, and the consent lookup silently returns NOTHING — which for a consent
        // provider means "unknown" on every question (the failure AccountTerritoryAssignment already hit). The embedded
        // value objects (evidence pointer, external references) carry no Guid FK except EvidenceRef.RefId, mapped below.
        Map<ConsentRecord>(map =>
        {
            map.GetMemberMap(r => r.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(r => r.ScopeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        Map<PreferenceRecord>(map => map.GetMemberMap(r => r.SubjectId).SetSerializer(stringGuid));
        Map<ConsentEvidenceRef>(map => map.GetMemberMap(e => e.RefId).SetSerializer(stringGuid));
        Map<ConsentExternalReference>(_ => { });

        // MOD-0165 FU04 — Campaign / CampaignTarget. Every Guid FK takes the string-Guid convention like every other CRM
        // aggregate: without it the campaign/target/brand/segment filters serialize a string while the stored value is
        // binary, and the lookup silently returns NOTHING (the failure AccountTerritoryAssignment already hit). For a
        // targeting runtime that would mean "this campaign has no targets" on every read — silently.
        Map<Campaign>(map =>
        {
            map.GetMemberMap(c => c.BrandId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.ProductId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.SubjectId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.TopicId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.ConceptChainTemplateId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.EngagementJourneyId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.DefaultKnowledgePathId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.DefaultKnowledgeContentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.OwnerUserId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            // FU08 — the cycle-period pin takes the same string-Guid convention. Without it the "campaigns bound to
            // this period" filter would serialize a string against a stored binary and silently match nothing.
            map.GetMemberMap(c => c.CyclePeriodId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            // FU09 - the legal-entity scope reference takes the same string-Guid convention as every other CRM FK.
            map.GetMemberMap(c => c.LegalEntityId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        // FU10 - the targeted-segment link. The segment id takes the same string-Guid convention as every other CRM
        // FK; without it the "campaigns targeting this segment" filter would serialize a string against stored binary
        // and silently match nothing.
        Map<CampaignTargetedSegment>(map =>
        {
            map.GetMemberMap(s => s.SegmentId).SetSerializer(stringGuid);
        });
        // FU11 - PriorityLevel needs NO entry here, and that is the point of the design rather than an omission.
        // It is a new string element, so auto-mapping stores and reads it correctly, and the deprecated int Priority
        // keeps its own element untouched. Retyping Priority in place would have required a custom int-or-string
        // serializer AND would have thrown on every pre-FU11 document until that serializer existed, so the field was
        // added beside the old one instead of over it. Nothing was migrated; see CampaignTarget.DerivedPriorityLevel.
        Map<CampaignTarget>(map =>
        {
            map.GetMemberMap(t => t.CampaignId).SetSerializer(stringGuid);
            map.GetMemberMap(t => t.TargetId).SetSerializer(stringGuid);
            map.GetMemberMap(t => t.SourceReferenceId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(t => t.SnapshotBatchId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        Map<CampaignTargetConsentEvaluation>(map =>
        {
            map.GetMemberMap(e => e.MatchedConsentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(e => e.MatchedPreferenceIds)
                .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
        });
        Map<CampaignExternalReference>(_ => { });

        // MOD-0162 FU02 — Knowledge (KnowledgeContent / Subject / Topic / AudienceProfile). Every Guid FK takes the
        // string-Guid convention like every other CRM aggregate: without it the subject/topic/brand/product filters
        // serialize a string while the stored value is binary, and the lookup silently returns NOTHING (the failure
        // AccountTerritoryAssignment already hit). SubjectId on content and Topic is a required Guid; the rest are
        // nullable references.
        Map<KnowledgeContent>(map =>
        {
            map.GetMemberMap(c => c.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(c => c.TopicId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.AudienceProfileId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.ConceptNodeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.BrandId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.ProductId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.CampaignId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(c => c.SegmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        Map<Subject>(map => map.GetMemberMap(s => s.ParentSubjectId)
            .SetSerializer(new NullableSerializer<Guid>(stringGuid)));
        Map<Topic>(map =>
        {
            map.GetMemberMap(t => t.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(t => t.ParentTopicId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        Map<AudienceProfile>(_ => { });
        Map<KnowledgeExternalReference>(_ => { });

        // MOD-0162 FU03 — Concept graph. Every Guid FK takes the string-Guid convention like every other CRM aggregate:
        // without it the subject/type/node/from/to/relationship filters serialize a string while the stored value is
        // binary, and the lookup silently returns NOTHING (the failure AccountTerritoryAssignment already hit). For a
        // graph that would mean "this subject has no nodes/edges" on every read — silently. OrderedConceptTypes is a
        // List<Guid> and needs the enumerable string-Guid serializer (like TerritoryRuleCriteria account-id lists).
        Map<ConceptType>(map => map.GetMemberMap(x => x.SubjectId).SetSerializer(stringGuid));
        Map<ConceptNode>(map =>
        {
            map.GetMemberMap(x => x.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(x => x.ConceptTypeId).SetSerializer(stringGuid);
        });
        Map<ConceptRelationship>(map =>
        {
            map.GetMemberMap(x => x.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(x => x.FromConceptNodeId).SetSerializer(stringGuid);
            map.GetMemberMap(x => x.ToConceptNodeId).SetSerializer(stringGuid);
        });
        Map<ConceptChainTemplate>(map =>
        {
            map.GetMemberMap(x => x.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(x => x.OrderedConceptTypes)
                .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
        });
        Map<KnowledgeContentConceptLink>(map =>
        {
            map.GetMemberMap(x => x.KnowledgeContentId).SetSerializer(stringGuid);
            map.GetMemberMap(x => x.ConceptNodeId).SetSerializer(stringGuid);
            map.GetMemberMap(x => x.ConceptRelationshipId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });

        // MOD-0162 FU04 — KnowledgePath (steps embedded, D2). Every Guid FK — on the root AND on the embedded step /
        // branch-condition types — takes the string-Guid convention like every other CRM aggregate: without it the
        // subject/topic/audience filters AND the embedded StepId/ContentId/ConceptNodeId/PrerequisiteStepId/TargetStepId
        // members serialize a string while the stored value is binary, and lookups silently return NOTHING (the
        // AccountTerritoryAssignment lesson). The embedded types MUST have their own class map registered or their Guid
        // members fall through to the global Standard serializer (binary sub-type 4).
        Map<KnowledgePath>(map =>
        {
            map.GetMemberMap(p => p.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(p => p.TopicId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.AudienceProfileId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(p => p.SupersedesPathId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(KnowledgePathStep)))
        {
            BsonClassMap.RegisterClassMap<KnowledgePathStep>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(s => s.StepId).SetSerializer(stringGuid);
                map.GetMemberMap(s => s.ContentId).SetSerializer(stringGuid);
                map.GetMemberMap(s => s.PrerequisiteStepId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(s => s.ConceptNodeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(KnowledgePathBranchCondition)))
        {
            BsonClassMap.RegisterClassMap<KnowledgePathBranchCondition>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(b => b.TargetStepId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }

        // MOD-0162 FU05 — ContentEngagementJourney (stages embedded, S2). Same rule as FU04: every Guid FK on the root
        // AND on the embedded stage / branch-condition types takes the string-Guid convention, otherwise
        // StageId/RecommendedKnowledgePathId/FallbackStageId/TargetStageId serialize as a string while the stored value
        // is binary and lookups silently return NOTHING (the AccountTerritoryAssignment lesson). The embedded types MUST
        // have their own class map registered or their Guid members fall through to the global Standard serializer.
        Map<ContentEngagementJourney>(map =>
        {
            map.GetMemberMap(j => j.SubjectId).SetSerializer(stringGuid);
            map.GetMemberMap(j => j.TopicId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(j => j.AudienceProfileId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(j => j.SupersedesJourneyId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(ContentEngagementJourneyStage)))
        {
            BsonClassMap.RegisterClassMap<ContentEngagementJourneyStage>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(s => s.StageId).SetSerializer(stringGuid);
                map.GetMemberMap(s => s.RecommendedKnowledgePathId).SetSerializer(stringGuid);
                map.GetMemberMap(s => s.FallbackStageId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(ContentEngagementJourneyBranchCondition)))
        {
            BsonClassMap.RegisterClassMap<ContentEngagementJourneyBranchCondition>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(b => b.TargetStageId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }

        // MOD-0167 FU02 - Segment (criteria embedded) + TargetCustomer. Every Guid FK takes the string-Guid
        // convention like every other CRM aggregate: without it the lineage / segment / subject filters serialize a
        // string while the stored value is binary and the lookup silently returns NOTHING (the
        // AccountTerritoryAssignment lesson). For a segmentation runtime that would mean "this segment has no members"
        // and "this person is in no segment" - silently, and with no error anywhere. The EMBEDDED criteria node MUST
        // have its own class map registered or its NodeId / ParentNodeId fall through to the global Standard
        // serializer (binary sub-type 4) and the tree stops linking up after a round-trip.
        Map<Segment>(map =>
        {
            map.GetMemberMap(s => s.VersionLineageId).SetSerializer(stringGuid);
            map.GetMemberMap(s => s.SupersededBySegmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(SegmentCriteriaNode)))
        {
            BsonClassMap.RegisterClassMap<SegmentCriteriaNode>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(n => n.NodeId).SetSerializer(stringGuid);
                map.GetMemberMap(n => n.ParentNodeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        Map<TargetCustomer>(map =>
        {
            map.GetMemberMap(t => t.SegmentId).SetSerializer(stringGuid);
            map.GetMemberMap(t => t.SubjectId).SetSerializer(stringGuid);
        });

        // MOD-0167 FU04 - StrategyTemplate and its FOUR embedded binding types. Every embedded type needs its OWN class
        // map or its Guid members fall through to the global Standard serializer (binary sub-type 4): the segment,
        // product, SKU and content ids would then be stored as binary while every filter serializes a string, and the
        // lookups would return NOTHING - silently (the AccountTerritoryAssignment lesson). For a playbook that would
        // read as "this play binds no segment and no product", with no error anywhere.
        Map<Domain.Entities.StrategyTemplate>(map =>
        {
            map.GetMemberMap(t => t.VersionLineageId).SetSerializer(stringGuid);
            map.GetMemberMap(t => t.SupersededByTemplateId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(StrategyTemplateSegmentBinding)))
        {
            BsonClassMap.RegisterClassMap<StrategyTemplateSegmentBinding>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(b => b.BindingId).SetSerializer(stringGuid);
                map.GetMemberMap(b => b.SegmentId).SetSerializer(stringGuid);
                map.GetMemberMap(b => b.SegmentLineageId).SetSerializer(stringGuid);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(StrategyTemplateFrequencyIntent)))
        {
            BsonClassMap.RegisterClassMap<StrategyTemplateFrequencyIntent>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(f => f.VisitFrequencyPolicyId)
                    .SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(StrategyTemplateProductLine)))
        {
            BsonClassMap.RegisterClassMap<StrategyTemplateProductLine>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(l => l.LineId).SetSerializer(stringGuid);
                map.GetMemberMap(l => l.GlobalProductId).SetSerializer(stringGuid);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(StrategyTemplateSkuAllocation)))
        {
            BsonClassMap.RegisterClassMap<StrategyTemplateSkuAllocation>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(a => a.AllocationId).SetSerializer(stringGuid);
                map.GetMemberMap(a => a.GskuId).SetSerializer(stringGuid);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(StrategyTemplateContentBinding)))
        {
            BsonClassMap.RegisterClassMap<StrategyTemplateContentBinding>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(c => c.BindingId).SetSerializer(stringGuid);
                map.GetMemberMap(c => c.ContentRefId).SetSerializer(stringGuid);
            });
        }

        // MOD-0165 FU06/FU07 - CyclePeriod. FU07 gave it a Guid member of its own (the legal-entity scope reference),
        // so it now needs the string-Guid serializer like its siblings: without it the id would be written as binary
        // while a filter serializes it as a string, and scope queries would come back silently EMPTY.
        Map<CyclePeriod>(map =>
        {
            map.GetMemberMap(p => p.LegalEntityId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
        });

        if (!BsonClassMap.IsClassMapRegistered(typeof(TerritoryRuleCriteria)))
        {
            BsonClassMap.RegisterClassMap<TerritoryRuleCriteria>(map =>
            {
                map.AutoMap();
                map.UnmapMember(c => c.IsEmpty);
                map.GetMemberMap(c => c.IncludeAccountIds)
                    .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
                map.GetMemberMap(c => c.ExcludeAccountIds)
                    .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
            });
        }

        // MOD-0155 FU06 - CycleCapacity and its EMBEDDED month rows. The aggregate carries a Guid member of its own
        // (the cycle-period pin), so it needs the string-Guid serializer like its siblings: without it the id would be
        // written as binary while a filter serializes it as a string, and every by-period query would come back
        // silently EMPTY. The embedded type is registered too - an embedded type omitted from the class maps is the
        // documented CRM trap.
        Map<CycleCapacity>(map =>
        {
            map.GetMemberMap(c => c.CyclePeriodId).SetSerializer(stringGuid);
            // MOD-0155 FU07 - FU06 stored ONE root Fte; FU07 moved it onto each month. Extra elements keep that old
            // value readable so EnsureMonthlyFte can copy it onto every month and an existing capacity keeps
            // producing the figure it always produced. Without this the driver would drop the field on read and an
            // old row would silently adopt today's configured average.
            map.MapExtraElementsProperty(nameof(CycleCapacity.LegacyElements));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(CycleCapacityMonth)))
        {
            BsonClassMap.RegisterClassMap<CycleCapacityMonth>(map => map.AutoMap());
        }

        // MOD-0155 FU01 - PlannedVisit and its SIX embedded types. Every Guid FK on the root AND on each embedded type
        // takes the string-Guid convention, or the ids (TargetId, Content.JourneyId/StageId/StrategyTemplateId,
        // Selection.SegmentId/CampaignId, Consent.MatchedConsentId/MatchedPreferenceIds, Frequency.SelectedFrequencyPolicyId)
        // serialize as a string while the stored value is binary and every by-id filter silently returns NOTHING (the
        // AccountTerritoryAssignment lesson). An embedded type omitted from the class maps is the documented CRM trap, so
        // every one is registered - even the two (ResourceRef, ScheduleSlot) that carry no Guid, so the driver never
        // treats them as anonymous. PlannedDate is a DateOnly stored as a "yyyy-MM-dd" STRING so it is sortable/indexable
        // without the DateTimeOffset parallel-arrays trap the whole DateOnly choice exists to avoid.
        var dateOnlyString = new Serialization.DateOnlyStringSerializer();
        Map<PlannedVisit>(map =>
        {
            map.GetMemberMap(v => v.TargetId).SetSerializer(stringGuid);
            map.GetMemberMap(v => v.AccountId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.ContactId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.AccountContactLinkId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.TerritoryNodeId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.TerritoryModelId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.CampaignId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.PositionId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            map.GetMemberMap(v => v.PlannedDate).SetSerializer(dateOnlyString);
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitResourceRef)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitResourceRef>(map => map.AutoMap());
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitScheduleSlot)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitScheduleSlot>(map =>
            {
                map.AutoMap();
                map.UnmapMember(s => s.IsPacked);
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitFrequencyProvenance)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitFrequencyProvenance>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(f => f.SelectedFrequencyPolicyId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitConsentProvenance)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitConsentProvenance>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(c => c.MatchedConsentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(c => c.MatchedPreferenceIds)
                    .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitContentRef)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitContentRef>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(c => c.JourneyId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(c => c.StageId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(c => c.StrategyTemplateId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitSelectionProvenance)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitSelectionProvenance>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(s => s.SegmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(s => s.CampaignId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(s => s.StrategyTemplateId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlannedVisitAvailabilitySnapshot)))
        {
            BsonClassMap.RegisterClassMap<PlannedVisitAvailabilitySnapshot>(map => map.AutoMap());
        }

        // MOD-0155 FU05 - PlanningSession (thin staging aggregate) and its THREE embedded types + one embedded contact.
        // Every Guid FK on the root AND on each embedded type takes the string-Guid convention, or the ids
        // (CyclePeriodId, Selection.SelectedAccountIds/SelectedPharmacyIds/SegmentId/CampaignId, the contact's
        // ContactId/AccountId/AccountContactLinkId, Provenance.SegmentId/CampaignId/StrategyTemplateId,
        // CommittedPlannedVisitIds) serialize as a string while the stored value is binary and every by-id filter
        // silently returns NOTHING (the AccountTerritoryAssignment lesson). An embedded type omitted from the class maps
        // is the documented CRM new-aggregate trap, so every one is registered - even the generation-state block that
        // carries no Guid, so the driver never treats it as anonymous. List<Guid> members use the enumerable string-Guid
        // serializer (like TerritoryRuleCriteria's account-id lists). No DateTimeOffset is a co-sorted index key.
        Map<PlanningSession>(map =>
        {
            map.GetMemberMap(s => s.CyclePeriodId).SetSerializer(stringGuid);
            map.GetMemberMap(s => s.CommittedPlannedVisitIds)
                .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlanningSessionSelection)))
        {
            BsonClassMap.RegisterClassMap<PlanningSessionSelection>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(s => s.SelectedAccountIds)
                    .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
                map.GetMemberMap(s => s.SelectedPharmacyIds)
                    .SetSerializer(new EnumerableInterfaceImplementerSerializer<List<Guid>, Guid>(stringGuid));
                map.GetMemberMap(s => s.SegmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(s => s.CampaignId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlanningSessionSelectedContact)))
        {
            BsonClassMap.RegisterClassMap<PlanningSessionSelectedContact>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(c => c.ContactId).SetSerializer(stringGuid);
                map.GetMemberMap(c => c.AccountId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(c => c.AccountContactLinkId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlanningSessionGenerationState)))
        {
            BsonClassMap.RegisterClassMap<PlanningSessionGenerationState>(map => map.AutoMap());
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(PlanningSessionProvenance)))
        {
            BsonClassMap.RegisterClassMap<PlanningSessionProvenance>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(p => p.SegmentId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(p => p.CampaignId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(p => p.StrategyTemplateId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }

        // MOD-0155 FU02 - VisitReport and its FOUR embedded types. The root carries a Guid FK of its own
        // (PlannedVisitId), and the embedded ContentActuals / Sample carry snapshot Guids (JourneyId, StageId, ItemId).
        // Every one takes the string-Guid convention, or the ids serialize as a string while the stored value is binary
        // and every by-id filter (the report-for-a-visit lookup, the calendar-join by plan-id set) silently returns
        // NOTHING (the AccountTerritoryAssignment lesson). An embedded type omitted from the class maps is the documented
        // CRM new-aggregate trap, so every one is registered - even Feedback / Amendment which carry no Guid, so the
        // driver never treats them as anonymous. RescheduleToDate is a DateOnly stored as a "yyyy-MM-dd" STRING (the FU01
        // pattern) so a date pairing never trips the DateTimeOffset parallel-arrays 500; ExecutedAt/SubmittedAt/AmendedAt
        // are lone DateTimeOffsets and are never co-sorted.
        Map<VisitReport>(map =>
        {
            map.GetMemberMap(r => r.PlannedVisitId).SetSerializer(stringGuid);
            map.GetMemberMap(r => r.RescheduleToDate).SetSerializer(
                new NullableSerializer<DateOnly>(dateOnlyString));
        });
        if (!BsonClassMap.IsClassMapRegistered(typeof(VisitReportContentActuals)))
        {
            BsonClassMap.RegisterClassMap<VisitReportContentActuals>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(c => c.JourneyId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
                map.GetMemberMap(c => c.StageId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(VisitReportSample)))
        {
            BsonClassMap.RegisterClassMap<VisitReportSample>(map =>
            {
                map.AutoMap();
                map.GetMemberMap(s => s.ItemId).SetSerializer(new NullableSerializer<Guid>(stringGuid));
            });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(VisitReportFeedback)))
        {
            BsonClassMap.RegisterClassMap<VisitReportFeedback>(map => map.AutoMap());
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(VisitReportAmendment)))
        {
            BsonClassMap.RegisterClassMap<VisitReportAmendment>(map => map.AutoMap());
        }

        _classMapsRegistered = true;
    }

    private static void Map<T>(Action<BsonClassMap<T>> configure)
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<T>(map =>
        {
            map.AutoMap();
            configure(map);
        });
    }

    private static void TryEnsureIndexes(IMongoDatabase database)
    {
        try
        {
            var accounts = database.GetCollection<Account>("accounts");
            accounts.Indexes.CreateOne(new CreateIndexModel<Account>(
                Builders<Account>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.AccountCode),
                new CreateIndexOptions<Account>
                {
                    Unique = true,
                    Name = "ux_accounts_tenant_code",
                    PartialFilterExpression = Builders<Account>.Filter.Eq(a => a.IsDeleted, false)
                }));
            accounts.Indexes.CreateOne(new CreateIndexModel<Account>(
                Builders<Account>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.AccountName),
                new CreateIndexOptions { Name = "ix_accounts_tenant_name" }));
            accounts.Indexes.CreateOne(new CreateIndexModel<Account>(
                Builders<Account>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.ParentAccountId),
                new CreateIndexOptions { Name = "ix_accounts_tenant_parent" }));

            var externalRefs = database.GetCollection<AccountExternalReference>("account_external_references");
            externalRefs.Indexes.CreateOne(new CreateIndexModel<AccountExternalReference>(
                Builders<AccountExternalReference>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.SourceSystem).Ascending(r => r.ExternalId),
                new CreateIndexOptions<AccountExternalReference>
                {
                    Unique = true,
                    Name = "ux_account_external_refs_tenant_source_external",
                    PartialFilterExpression = Builders<AccountExternalReference>.Filter.Eq(r => r.IsDeleted, false)
                }));

            var attributes = database.GetCollection<AccountAttributeValue>("account_attribute_values");
            attributes.Indexes.CreateOne(new CreateIndexModel<AccountAttributeValue>(
                Builders<AccountAttributeValue>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.AccountId).Ascending(a => a.AttributeCode),
                new CreateIndexOptions { Unique = true, Name = "ux_account_attributes_tenant_account_code" }));

            var sequences = database.GetCollection<AccountCodeSequence>("account_code_sequences");
            sequences.Indexes.CreateOne(new CreateIndexModel<AccountCodeSequence>(
                Builders<AccountCodeSequence>.IndexKeys.Ascending(s => s.TenantId).Ascending(s => s.Year),
                new CreateIndexOptions { Unique = true, Name = "ux_account_code_sequences_tenant_year" }));

            // MOD-0150 FU01 — Contact indexes (soft-delete aware; tenant-scoped).
            var contacts = database.GetCollection<Contact>("contacts");
            contacts.Indexes.CreateOne(new CreateIndexModel<Contact>(
                Builders<Contact>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.DisplayName),
                new CreateIndexOptions { Name = "ix_contacts_tenant_display" }));
            contacts.Indexes.CreateOne(new CreateIndexModel<Contact>(
                Builders<Contact>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.ContactType),
                new CreateIndexOptions { Name = "ix_contacts_tenant_type" }));
            contacts.Indexes.CreateOne(new CreateIndexModel<Contact>(
                Builders<Contact>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.IsDeleted).Ascending(c => c.Status),
                new CreateIndexOptions { Name = "ix_contacts_tenant_deleted_status" }));
            // MOD-0150 Contact Location Hardening — country index prepares cross-country checks + MOD-0151/MOD-0155
            // location/availability queries (sparse: only contacts that carry a country).
            contacts.Indexes.CreateOne(new CreateIndexModel<Contact>(
                Builders<Contact>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.CountryRef),
                new CreateIndexOptions { Name = "ix_contacts_tenant_country", Sparse = true }));

            var contactRefs = database.GetCollection<ContactExternalReference>("contact_external_references");
            contactRefs.Indexes.CreateOne(new CreateIndexModel<ContactExternalReference>(
                Builders<ContactExternalReference>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.SourceSystem).Ascending(r => r.ExternalId),
                new CreateIndexOptions<ContactExternalReference>
                {
                    Unique = true,
                    Name = "ux_contact_external_refs_tenant_source_external",
                    PartialFilterExpression = Builders<ContactExternalReference>.Filter.Eq(r => r.IsDeleted, false)
                }));
            contactRefs.Indexes.CreateOne(new CreateIndexModel<ContactExternalReference>(
                Builders<ContactExternalReference>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.ContactId),
                new CreateIndexOptions { Name = "ix_contact_external_refs_tenant_contact" }));

            // MOD-0150 FU03 — AccountContactLink indexes (soft-delete aware).
            var links = database.GetCollection<AccountContactLink>("account_contact_links");
            // MOD-0149/MOD-0150 historical lifecycle: uniqueness must apply only to ACTIVE (Status="active") non-deleted
            // records so an ended/inactive record (kept for history) never blocks a new active one. Drop the legacy
            // IsDeleted-only partial unique indexes so they are recreated below with the Status predicate (idempotent).
            DropIndexIfExists(links, "ux_account_contact_links_active_natural");
            DropIndexIfExists(links, "ux_account_contact_links_primary");
            links.Indexes.CreateOne(new CreateIndexModel<AccountContactLink>(
                Builders<AccountContactLink>.IndexKeys.Ascending(l => l.TenantId).Ascending(l => l.AccountId).Ascending(l => l.IsDeleted),
                new CreateIndexOptions { Name = "ix_account_contact_links_tenant_account" }));
            links.Indexes.CreateOne(new CreateIndexModel<AccountContactLink>(
                Builders<AccountContactLink>.IndexKeys.Ascending(l => l.TenantId).Ascending(l => l.ContactId).Ascending(l => l.IsDeleted),
                new CreateIndexOptions { Name = "ix_account_contact_links_tenant_contact" }));
            links.Indexes.CreateOne(new CreateIndexModel<AccountContactLink>(
                Builders<AccountContactLink>.IndexKeys
                    .Ascending(l => l.TenantId).Ascending(l => l.AccountId).Ascending(l => l.ContactId).Ascending(l => l.RoleCode),
                new CreateIndexOptions<AccountContactLink>
                {
                    Unique = true,
                    Name = "ux_account_contact_links_active_natural",
                    PartialFilterExpression = Builders<AccountContactLink>.Filter.And(
                        Builders<AccountContactLink>.Filter.Eq(l => l.IsDeleted, false),
                        Builders<AccountContactLink>.Filter.Eq(l => l.Status, "active"))
                }));
            links.Indexes.CreateOne(new CreateIndexModel<AccountContactLink>(
                Builders<AccountContactLink>.IndexKeys
                    .Ascending(l => l.TenantId).Ascending(l => l.AccountId).Ascending(l => l.RoleCode),
                new CreateIndexOptions<AccountContactLink>
                {
                    Unique = true,
                    Name = "ux_account_contact_links_primary",
                    PartialFilterExpression = Builders<AccountContactLink>.Filter.And(
                        Builders<AccountContactLink>.Filter.Eq(l => l.IsDeleted, false),
                        Builders<AccountContactLink>.Filter.Eq(l => l.IsPrimary, true),
                        Builders<AccountContactLink>.Filter.Eq(l => l.Status, "active"))
                }));

            // MOD-0150 FU07 — availability master indexes (soft-delete aware, tenant scoped). Overlap cannot be
            // expressed as an index (it is a range test), so it is enforced in the handler; the unique index below
            // only covers the EXACT natural key so a concurrent double-post cannot create a true duplicate. Partial
            // filters use Eq only — $ne/$not is unsupported in a partial index filter and crash-loops the service.
            var availabilities = database.GetCollection<ContactAvailability>("contact_availabilities");
            availabilities.Indexes.CreateOne(new CreateIndexModel<ContactAvailability>(
                Builders<ContactAvailability>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.AccountContactLinkId).Ascending(a => a.IsDeleted),
                new CreateIndexOptions { Name = "ix_contact_availabilities_tenant_link" }));
            availabilities.Indexes.CreateOne(new CreateIndexModel<ContactAvailability>(
                Builders<ContactAvailability>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.ContactId).Ascending(a => a.IsDeleted),
                new CreateIndexOptions { Name = "ix_contact_availabilities_tenant_contact" }));
            availabilities.Indexes.CreateOne(new CreateIndexModel<ContactAvailability>(
                Builders<ContactAvailability>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.AccountId).Ascending(a => a.IsDeleted),
                new CreateIndexOptions { Name = "ix_contact_availabilities_tenant_account" }));
            availabilities.Indexes.CreateOne(new CreateIndexModel<ContactAvailability>(
                Builders<ContactAvailability>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.AccountContactLinkId)
                    .Ascending(a => a.Weekday).Ascending(a => a.StartTime).Ascending(a => a.EndTime),
                new CreateIndexOptions<ContactAvailability>
                {
                    Unique = true,
                    Name = "ux_contact_availabilities_active_natural",
                    PartialFilterExpression = Builders<ContactAvailability>.Filter.And(
                        Builders<ContactAvailability>.Filter.Eq(a => a.IsDeleted, false),
                        Builders<ContactAvailability>.Filter.Eq(a => a.Status, AvailabilityLifecycle.Active))
                }));

            // MOD-0150 FU07 — date-specific exception indexes. One ACTIVE exception per (link, date) is a real
            // natural key, so it is enforced both in the handler (controlled 409) and here (race safety).
            var availabilityExceptions = database.GetCollection<ContactAvailabilityException>("contact_availability_exceptions");
            availabilityExceptions.Indexes.CreateOne(new CreateIndexModel<ContactAvailabilityException>(
                Builders<ContactAvailabilityException>.IndexKeys
                    .Ascending(e => e.TenantId).Ascending(e => e.AccountContactLinkId).Ascending(e => e.Date),
                new CreateIndexOptions { Name = "ix_contact_availability_exceptions_tenant_link_date" }));
            availabilityExceptions.Indexes.CreateOne(new CreateIndexModel<ContactAvailabilityException>(
                Builders<ContactAvailabilityException>.IndexKeys
                    .Ascending(e => e.TenantId).Ascending(e => e.ContactId).Ascending(e => e.IsDeleted),
                new CreateIndexOptions { Name = "ix_contact_availability_exceptions_tenant_contact" }));
            availabilityExceptions.Indexes.CreateOne(new CreateIndexModel<ContactAvailabilityException>(
                Builders<ContactAvailabilityException>.IndexKeys
                    .Ascending(e => e.TenantId).Ascending(e => e.AccountId).Ascending(e => e.IsDeleted),
                new CreateIndexOptions { Name = "ix_contact_availability_exceptions_tenant_account" }));
            availabilityExceptions.Indexes.CreateOne(new CreateIndexModel<ContactAvailabilityException>(
                Builders<ContactAvailabilityException>.IndexKeys
                    .Ascending(e => e.TenantId).Ascending(e => e.AccountContactLinkId).Ascending(e => e.Date),
                new CreateIndexOptions<ContactAvailabilityException>
                {
                    Unique = true,
                    Name = "ux_contact_availability_exceptions_active_date",
                    PartialFilterExpression = Builders<ContactAvailabilityException>.Filter.And(
                        Builders<ContactAvailabilityException>.Filter.Eq(e => e.IsDeleted, false),
                        Builders<ContactAvailabilityException>.Filter.Eq(e => e.Status, AvailabilityLifecycle.Active))
                }));

            // MOD-0150 FU04 — AccountRelationship indexes (soft-delete aware). Bidirectional duplicate is enforced
            // at the repository level (checks both directions); the unique index covers the exact directional pair.
            var rels = database.GetCollection<AccountRelationship>("account_relationships");
            DropIndexIfExists(rels, "ux_account_relationships_active_directional");
            rels.Indexes.CreateOne(new CreateIndexModel<AccountRelationship>(
                Builders<AccountRelationship>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.SourceAccountId).Ascending(r => r.IsDeleted),
                new CreateIndexOptions { Name = "ix_account_relationships_tenant_source" }));
            rels.Indexes.CreateOne(new CreateIndexModel<AccountRelationship>(
                Builders<AccountRelationship>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.TargetAccountId).Ascending(r => r.IsDeleted),
                new CreateIndexOptions { Name = "ix_account_relationships_tenant_target" }));
            rels.Indexes.CreateOne(new CreateIndexModel<AccountRelationship>(
                Builders<AccountRelationship>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.SourceAccountId).Ascending(r => r.TargetAccountId).Ascending(r => r.RelationshipType),
                new CreateIndexOptions<AccountRelationship>
                {
                    Unique = true,
                    Name = "ux_account_relationships_active_directional",
                    PartialFilterExpression = Builders<AccountRelationship>.Filter.And(
                        Builders<AccountRelationship>.Filter.Eq(r => r.IsDeleted, false),
                        Builders<AccountRelationship>.Filter.Eq(r => r.Status, "active"))
                }));

            // MOD-0151 FU01 — Territory model indexes (soft-delete aware; tenant-scoped unique ModelCode).
            var territoryModels = database.GetCollection<TerritoryModel>("territory_models");
            territoryModels.Indexes.CreateOne(new CreateIndexModel<TerritoryModel>(
                Builders<TerritoryModel>.IndexKeys.Ascending(m => m.TenantId).Ascending(m => m.ModelCode),
                new CreateIndexOptions<TerritoryModel>
                {
                    Unique = true,
                    Name = "ux_territory_models_tenant_code",
                    PartialFilterExpression = Builders<TerritoryModel>.Filter.Eq(m => m.IsDeleted, false)
                }));
            territoryModels.Indexes.CreateOne(new CreateIndexModel<TerritoryModel>(
                Builders<TerritoryModel>.IndexKeys.Ascending(m => m.TenantId).Ascending(m => m.Status),
                new CreateIndexOptions { Name = "ix_territory_models_tenant_status" }));

            // MOD-0151 FU01 — Territory node indexes (TerritoryCode unique within a model; parent lookup).
            var territoryNodes = database.GetCollection<TerritoryNode>("territory_nodes");
            territoryNodes.Indexes.CreateOne(new CreateIndexModel<TerritoryNode>(
                Builders<TerritoryNode>.IndexKeys.Ascending(n => n.TenantId).Ascending(n => n.ModelId).Ascending(n => n.TerritoryCode),
                new CreateIndexOptions<TerritoryNode>
                {
                    Unique = true,
                    Name = "ux_territory_nodes_tenant_model_code",
                    PartialFilterExpression = Builders<TerritoryNode>.Filter.Eq(n => n.IsDeleted, false)
                }));
            territoryNodes.Indexes.CreateOne(new CreateIndexModel<TerritoryNode>(
                Builders<TerritoryNode>.IndexKeys.Ascending(n => n.TenantId).Ascending(n => n.ModelId).Ascending(n => n.ParentTerritoryId),
                new CreateIndexOptions { Name = "ix_territory_nodes_tenant_model_parent" }));

            // MOD-0151 FU05 — model/account history and current-coverage query indexes. No unique active index:
            // business-scope/date overlap is a transactional domain guard, not a scalar Mongo uniqueness rule.
            var accountTerritoryAssignments =
                database.GetCollection<AccountTerritoryAssignment>("account_territory_assignments");
            accountTerritoryAssignments.Indexes.CreateOne(new CreateIndexModel<AccountTerritoryAssignment>(
                Builders<AccountTerritoryAssignment>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.TerritoryModelId)
                    .Ascending(a => a.AccountId).Descending(a => a.EffectiveFrom),
                new CreateIndexOptions { Name = "ix_account_territory_history_by_model" }));
            // EffectiveFrom/EffectiveTo are DateTimeOffset, which this codebase serializes as a BSON array
            // ([ticks, offset]). A compound index may include AT MOST ONE array field — indexing both trips
            // Mongo's "cannot index parallel arrays" (code 171) on insert. EffectiveTo is dropped from the key;
            // the coverage query filters the (already narrow) per-account result on EffectiveTo in memory.
            accountTerritoryAssignments.Indexes.CreateOne(new CreateIndexModel<AccountTerritoryAssignment>(
                Builders<AccountTerritoryAssignment>.IndexKeys
                    .Ascending(a => a.TenantId).Ascending(a => a.AccountId)
                    .Ascending(a => a.AssignmentStatus).Ascending(a => a.EffectiveFrom),
                new CreateIndexOptions { Name = "ix_account_territory_current_coverage" }));

            // MOD-0151 FU04B — plan baseline lookup (latest version per model, then per-resource fan-in). No
            // DateTimeOffset member is indexed: CapturedAt / PlannedEffectiveFrom serialize as BSON arrays and a
            // compound key may hold at most one array field ("cannot index parallel arrays").
            var planSnapshots = database.GetCollection<TerritoryResourceAssignmentPlanSnapshot>(
                TerritoryResourceAssignmentPlanSnapshotRepository.CollectionName);
            planSnapshots.Indexes.CreateOne(new CreateIndexModel<TerritoryResourceAssignmentPlanSnapshot>(
                Builders<TerritoryResourceAssignmentPlanSnapshot>.IndexKeys
                    .Ascending(s => s.TenantId).Ascending(s => s.TerritoryModelId).Descending(s => s.SnapshotVersion),
                new CreateIndexOptions { Name = "ix_territory_plan_snapshots_tenant_model_version" }));
            planSnapshots.Indexes.CreateOne(new CreateIndexModel<TerritoryResourceAssignmentPlanSnapshot>(
                Builders<TerritoryResourceAssignmentPlanSnapshot>.IndexKeys
                    .Ascending(s => s.TenantId).Ascending("Lines.ResourceId"),
                new CreateIndexOptions { Name = "ix_territory_plan_snapshots_tenant_resource" }));

            // MOD-0151 FU08 — import run history lookups (per model, and per re-uploaded file hash). UploadedAt is a
            // DateTimeOffset (BSON array) and is deliberately NOT part of any key: a compound index may hold at most
            // one array field. Ordering is done in memory by the repository.
            var importRuns = database.GetCollection<TerritoryImportRun>(TerritoryImportRunRepository.CollectionName);
            importRuns.Indexes.CreateOne(new CreateIndexModel<TerritoryImportRun>(
                Builders<TerritoryImportRun>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.TerritoryModelId),
                new CreateIndexOptions { Name = "ix_territory_import_runs_tenant_model" }));
            importRuns.Indexes.CreateOne(new CreateIndexModel<TerritoryImportRun>(
                Builders<TerritoryImportRun>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.TerritoryModelId).Ascending(r => r.FileHash),
                new CreateIndexOptions { Name = "ix_territory_import_runs_tenant_model_hash" }));

            // MOD-0165 FU03 — Visit Frequency / Call-Cycle Policy indexes (soft-delete aware, tenant scoped). The
            // resolve seam looks policies up by (target, status); the unique index enforces one non-archived policy
            // per PolicyCode. EffectiveFrom / EffectiveTo are DateTimeOffset (BSON array) and are deliberately NOT
            // part of any key — a compound index may hold at most one array field ("cannot index parallel arrays"),
            // and the effective window is filtered in memory by the resolve engine. Partial filters use Eq only —
            // $ne/$not is unsupported in a partial-index filter and crash-loops the service.
            var frequencyPolicies =
                database.GetCollection<VisitFrequencyPolicy>(VisitFrequencyPolicyRepository.CollectionName);
            frequencyPolicies.Indexes.CreateOne(new CreateIndexModel<VisitFrequencyPolicy>(
                Builders<VisitFrequencyPolicy>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.TargetId).Ascending(p => p.Status),
                new CreateIndexOptions { Name = "ix_visit_frequency_policies_tenant_target_status" }));
            frequencyPolicies.Indexes.CreateOne(new CreateIndexModel<VisitFrequencyPolicy>(
                Builders<VisitFrequencyPolicy>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.TargetType).Ascending(p => p.TargetId),
                new CreateIndexOptions { Name = "ix_visit_frequency_policies_tenant_targettype_target" }));
            // PolicyCode uniqueness is NOT a DB unique index: an archived code is reusable, and a partial filter
            // cannot express "not archived" ($ne is unsupported in a partial-index filter). The duplicate-code guard
            // is enforced in the create handler (GetActiveByCodeAsync ignores archived → controlled 409). This index
            // is the lookup that guard rides on.
            frequencyPolicies.Indexes.CreateOne(new CreateIndexModel<VisitFrequencyPolicy>(
                Builders<VisitFrequencyPolicy>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.PolicyCode),
                new CreateIndexOptions { Name = "ix_visit_frequency_policies_tenant_code" }));

            // MOD-0164 FU02 — consent / preference indexes (soft-delete aware, tenant scoped). The evaluation seam
            // looks records up by (subject, channel); the external-reference index backs the duplicate-mapping guard.
            // EffectiveFrom / EffectiveTo / ArchivedAt are DateTimeOffset (BSON array) and are deliberately NOT part of
            // any key — a compound index may hold at most one array field ("cannot index parallel arrays") — and the
            // effective window is filtered in memory by the evaluation engine. No partial filter is used here: $ne/$not
            // is unsupported in a partial-index filter and crash-loops the service at startup.
            var consents = database.GetCollection<ConsentRecord>(ConsentRecordRepository.CollectionName);
            consents.Indexes.CreateOne(new CreateIndexModel<ConsentRecord>(
                Builders<ConsentRecord>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.SubjectType).Ascending(r => r.SubjectId)
                    .Ascending(r => r.Channel),
                new CreateIndexOptions { Name = "ix_consent_records_tenant_subject_channel" }));
            consents.Indexes.CreateOne(new CreateIndexModel<ConsentRecord>(
                Builders<ConsentRecord>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.Channel).Ascending(r => r.Purpose)
                    .Ascending(r => r.ConsentStatus),
                new CreateIndexOptions { Name = "ix_consent_records_tenant_channel_purpose_status" }));
            consents.Indexes.CreateOne(new CreateIndexModel<ConsentRecord>(
                Builders<ConsentRecord>.IndexKeys
                    .Ascending(r => r.TenantId)
                    .Ascending("ExternalReferences.SourceSystem")
                    .Ascending("ExternalReferences.ExternalId"),
                new CreateIndexOptions { Name = "ix_consent_records_tenant_external_ref" }));

            var preferences = database.GetCollection<PreferenceRecord>(PreferenceRecordRepository.CollectionName);
            preferences.Indexes.CreateOne(new CreateIndexModel<PreferenceRecord>(
                Builders<PreferenceRecord>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.SubjectType).Ascending(r => r.SubjectId),
                new CreateIndexOptions { Name = "ix_preference_records_tenant_subject" }));
            preferences.Indexes.CreateOne(new CreateIndexModel<PreferenceRecord>(
                Builders<PreferenceRecord>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.Channel).Ascending(r => r.PreferenceType),
                new CreateIndexOptions { Name = "ix_preference_records_tenant_channel_type" }));
            preferences.Indexes.CreateOne(new CreateIndexModel<PreferenceRecord>(
                Builders<PreferenceRecord>.IndexKeys
                    .Ascending(r => r.TenantId)
                    .Ascending("ExternalReferences.SourceSystem")
                    .Ascending("ExternalReferences.ExternalId"),
                new CreateIndexOptions { Name = "ix_preference_records_tenant_external_ref" }));

            // MOD-0165 FU04 — campaign / campaign target indexes (soft-delete aware, tenant scoped). The snapshot
            // idempotency lookup rides ix_campaign_targets_tenant_campaign_target. StartDate / EndDate / EffectiveFrom /
            // EffectiveTo / ArchivedAt are DateTimeOffset (BSON array) and are deliberately NOT part of any key — a
            // compound index may hold at most one array field ("cannot index parallel arrays") — and ordering is done in
            // memory. No partial filter is used: $ne/$not is unsupported in a partial-index filter and crash-loops the
            // service at startup, and CampaignCode uniqueness is enforced in the create handler instead (an archived
            // code is reusable, which a partial filter could not express).
            var campaigns = database.GetCollection<Campaign>(CampaignRepository.CollectionName);
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.CampaignCode),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_code" }));
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys
                    .Ascending(c => c.TenantId).Ascending(c => c.CampaignStatus).Ascending(c => c.CampaignType),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_status_type" }));
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys
                    .Ascending(c => c.TenantId)
                    .Ascending("ExternalReferences.SourceSystem")
                    .Ascending("ExternalReferences.ExternalId"),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_external_ref" }));
            // FU08 - the cycle-period pin. Guid + tenant only: StartDate/EndDate are DateTimeOffset (BSON arrays) and
            // adding either one here would recreate the parallel-array trap the comment above warns about.
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.CyclePeriodId),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_cycle_period" }));
            // FU09 - the campaign's address. Scalars only: StartDate/EndDate are DateTimeOffset (BSON arrays) and
            // adding either would recreate the parallel-array trap the comment above warns about.
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys
                    .Ascending(c => c.TenantId).Ascending(c => c.ScopeType).Ascending(c => c.BusinessUnitId),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_scope" }));
            // FU10 - how a campaign is targeted (list filter + column).
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.TargetingMode),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_targeting_mode" }));
            // FU10 - "which campaigns target this segment?". TargetedSegments is already an ARRAY, so this index may
            // hold no second array field: LinkedAt is a DateTimeOffset (a BSON array in this service) and is
            // deliberately absent, or Mongo would refuse with "cannot index parallel arrays".
            campaigns.Indexes.CreateOne(new CreateIndexModel<Campaign>(
                Builders<Campaign>.IndexKeys
                    .Ascending(c => c.TenantId).Ascending("TargetedSegments.SegmentId"),
                new CreateIndexOptions { Name = "ix_campaigns_tenant_targeted_segment" }));

            // MOD-0165 FU06 - CyclePeriod. Integer keys only: StartDate/EndDate are DateTimeOffset and therefore BSON
            // arrays, so indexing two of them together is the parallel-array trap. Code and (year, sequence)
            // uniqueness are enforced in the handlers - a partial filter cannot express "closed rows still hold their
            // code" and $ne in a partial-index filter crash-loops the service at startup.
            var cyclePeriods = database.GetCollection<CyclePeriod>(CyclePeriodRepository.CollectionName);
            cyclePeriods.Indexes.CreateOne(new CreateIndexModel<CyclePeriod>(
                Builders<CyclePeriod>.IndexKeys.Ascending(p => p.TenantId).Ascending(p => p.CycleCode),
                new CreateIndexOptions { Name = "ix_cycle_periods_tenant_code" }));
            cyclePeriods.Indexes.CreateOne(new CreateIndexModel<CyclePeriod>(
                Builders<CyclePeriod>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.CycleStatus).Descending(p => p.Year),
                new CreateIndexOptions { Name = "ix_cycle_periods_tenant_status_year" }));
            cyclePeriods.Indexes.CreateOne(new CreateIndexModel<CyclePeriod>(
                Builders<CyclePeriod>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.Year).Ascending(p => p.SequenceInYear),
                new CreateIndexOptions { Name = "ix_cycle_periods_tenant_year_sequence" }));

            // MOD-0155 FU06 - CycleCapacity. The 1:1 pin is enforced in the DATABASE as well as in the handler: the
            // handler gives the readable error, this index is the guarantee a concurrent second create cannot win.
            // The partial filter uses EQUALITY only (IsDeleted:false, IsArchived:false) - $ne / $not in a partial-index
            // filter crash-loops the service at startup, and archiving a capacity is what frees its period.
            // Scalar keys only: CreatedAt/UpdatedAt are DateTimeOffset (BSON arrays) and indexing two of them together
            // is the parallel-array trap.
            var cycleCapacities = database.GetCollection<CycleCapacity>(CycleCapacityRepository.CollectionName);
            cycleCapacities.Indexes.CreateOne(new CreateIndexModel<CycleCapacity>(
                Builders<CycleCapacity>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.CyclePeriodId),
                new CreateIndexOptions<CycleCapacity>
                {
                    Name = "ux_cycle_capacities_tenant_cycle_period",
                    Unique = true,
                    PartialFilterExpression = Builders<CycleCapacity>.Filter.And(
                        Builders<CycleCapacity>.Filter.Eq(c => c.IsDeleted, false),
                        Builders<CycleCapacity>.Filter.Eq(c => c.IsArchived, false))
                }));
            cycleCapacities.Indexes.CreateOne(new CreateIndexModel<CycleCapacity>(
                Builders<CycleCapacity>.IndexKeys
                    .Ascending(c => c.TenantId).Ascending(c => c.CalendarCountryCode),
                new CreateIndexOptions { Name = "ix_cycle_capacities_tenant_country" }));

            var campaignTargets = database.GetCollection<CampaignTarget>(CampaignTargetRepository.CollectionName);
            campaignTargets.Indexes.CreateOne(new CreateIndexModel<CampaignTarget>(
                Builders<CampaignTarget>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.CampaignId).Ascending(t => t.TargetType)
                    .Ascending(t => t.TargetId),
                new CreateIndexOptions { Name = "ix_campaign_targets_tenant_campaign_target" }));
            campaignTargets.Indexes.CreateOne(new CreateIndexModel<CampaignTarget>(
                Builders<CampaignTarget>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.CampaignId).Ascending(t => t.TargetStatus),
                new CreateIndexOptions { Name = "ix_campaign_targets_tenant_campaign_status" }));
            campaignTargets.Indexes.CreateOne(new CreateIndexModel<CampaignTarget>(
                Builders<CampaignTarget>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.SnapshotBatchId),
                new CreateIndexOptions { Name = "ix_campaign_targets_tenant_batch" }));

            // MOD-0162 FU02 — knowledge indexes (soft-delete aware, tenant scoped). EffectiveFrom / EffectiveTo /
            // ArchivedAt are DateTimeOffset (BSON array) and are deliberately NOT part of any key — a compound index may
            // hold at most one array field ("cannot index parallel arrays") — and ordering is done in memory. No partial
            // filter is used ($ne/$not is unsupported in a partial-index filter and crash-loops the service at startup);
            // code uniqueness is enforced in the create handlers instead (an archived code is reusable).
            var knowledgeContents = database.GetCollection<KnowledgeContent>(KnowledgeContentRepository.CollectionName);
            knowledgeContents.Indexes.CreateOne(new CreateIndexModel<KnowledgeContent>(
                Builders<KnowledgeContent>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.ContentCode),
                new CreateIndexOptions { Name = "ix_knowledge_contents_tenant_code" }));
            knowledgeContents.Indexes.CreateOne(new CreateIndexModel<KnowledgeContent>(
                Builders<KnowledgeContent>.IndexKeys
                    .Ascending(c => c.TenantId).Ascending(c => c.SubjectId).Ascending(c => c.ContentStatus),
                new CreateIndexOptions { Name = "ix_knowledge_contents_tenant_subject_status" }));
            knowledgeContents.Indexes.CreateOne(new CreateIndexModel<KnowledgeContent>(
                Builders<KnowledgeContent>.IndexKeys
                    .Ascending(c => c.TenantId)
                    .Ascending("ExternalReferences.SourceSystem")
                    .Ascending("ExternalReferences.ExternalId"),
                new CreateIndexOptions { Name = "ix_knowledge_contents_tenant_external_ref" }));

            var knowledgeSubjects = database.GetCollection<Subject>(SubjectRepository.CollectionName);
            knowledgeSubjects.Indexes.CreateOne(new CreateIndexModel<Subject>(
                Builders<Subject>.IndexKeys.Ascending(s => s.TenantId).Ascending(s => s.SubjectCode),
                new CreateIndexOptions { Name = "ix_knowledge_subjects_tenant_code" }));

            var knowledgeTopics = database.GetCollection<Topic>(TopicRepository.CollectionName);
            knowledgeTopics.Indexes.CreateOne(new CreateIndexModel<Topic>(
                Builders<Topic>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.SubjectId).Ascending(t => t.TopicCode),
                new CreateIndexOptions { Name = "ix_knowledge_topics_tenant_subject_code" }));
            knowledgeTopics.Indexes.CreateOne(new CreateIndexModel<Topic>(
                Builders<Topic>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.SubjectId).Ascending(t => t.ParentTopicId),
                new CreateIndexOptions { Name = "ix_knowledge_topics_tenant_subject_parent" }));

            var knowledgeProfiles = database.GetCollection<AudienceProfile>(AudienceProfileRepository.CollectionName);
            knowledgeProfiles.Indexes.CreateOne(new CreateIndexModel<AudienceProfile>(
                Builders<AudienceProfile>.IndexKeys.Ascending(p => p.TenantId).Ascending(p => p.ProfileCode),
                new CreateIndexOptions { Name = "ix_knowledge_audience_profiles_tenant_code" }));

            // MOD-0162 FU03 — concept-graph indexes (tenant scoped, soft-delete aware). EffectiveFrom / EffectiveTo /
            // ArchivedAt are DateTimeOffset (BSON array) and are deliberately NOT index keys (parallel-array trap); code
            // uniqueness is enforced in the create handlers (an archived code is reusable), so no partial $ne filter.
            var conceptTypes = database.GetCollection<ConceptType>(ConceptTypeRepository.CollectionName);
            conceptTypes.Indexes.CreateOne(new CreateIndexModel<ConceptType>(
                Builders<ConceptType>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.SubjectId).Ascending(x => x.ConceptTypeCode),
                new CreateIndexOptions { Name = "ix_concept_types_tenant_subject_code" }));

            var conceptNodes = database.GetCollection<ConceptNode>(ConceptNodeRepository.CollectionName);
            conceptNodes.Indexes.CreateOne(new CreateIndexModel<ConceptNode>(
                Builders<ConceptNode>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.SubjectId).Ascending(x => x.ConceptTypeId)
                    .Ascending(x => x.ConceptNodeCode),
                new CreateIndexOptions { Name = "ix_concept_nodes_tenant_subject_type_code" }));

            var conceptRelationships =
                database.GetCollection<ConceptRelationship>(ConceptRelationshipRepository.CollectionName);
            conceptRelationships.Indexes.CreateOne(new CreateIndexModel<ConceptRelationship>(
                Builders<ConceptRelationship>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.SubjectId).Ascending(x => x.FromConceptNodeId),
                new CreateIndexOptions { Name = "ix_concept_relationships_tenant_subject_from" }));

            var conceptChainTemplates =
                database.GetCollection<ConceptChainTemplate>(ConceptChainTemplateRepository.CollectionName);
            conceptChainTemplates.Indexes.CreateOne(new CreateIndexModel<ConceptChainTemplate>(
                Builders<ConceptChainTemplate>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.SubjectId).Ascending(x => x.ChainCode),
                new CreateIndexOptions { Name = "ix_concept_chain_templates_tenant_subject_code" }));

            var contentConceptLinks =
                database.GetCollection<KnowledgeContentConceptLink>(KnowledgeContentConceptLinkRepository.CollectionName);
            contentConceptLinks.Indexes.CreateOne(new CreateIndexModel<KnowledgeContentConceptLink>(
                Builders<KnowledgeContentConceptLink>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.KnowledgeContentId),
                new CreateIndexOptions { Name = "ix_content_concept_links_tenant_content" }));
            contentConceptLinks.Indexes.CreateOne(new CreateIndexModel<KnowledgeContentConceptLink>(
                Builders<KnowledgeContentConceptLink>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.ConceptNodeId),
                new CreateIndexOptions { Name = "ix_content_concept_links_tenant_node" }));

            // MOD-0162 FU04 — knowledge_paths (one collection; steps embedded). EffectiveFrom / EffectiveTo /
            // ArchivedAt / StepSetFrozenAt are DateTimeOffset (BSON array) and are deliberately NOT index keys
            // (parallel-array trap); in-array StepOrder/StepCode uniqueness cannot be an index (the handler is the
            // defence) and (PathCode, PathVersion) uniqueness is enforced in the create handler (an archived code is
            // reusable), so no partial $ne filter that would crash-loop the service.
            var knowledgePaths = database.GetCollection<KnowledgePath>(KnowledgePathRepository.CollectionName);
            knowledgePaths.Indexes.CreateOne(new CreateIndexModel<KnowledgePath>(
                Builders<KnowledgePath>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.PathCode).Ascending(p => p.PathVersion),
                new CreateIndexOptions { Name = "ix_knowledge_paths_tenant_code_version" }));
            knowledgePaths.Indexes.CreateOne(new CreateIndexModel<KnowledgePath>(
                Builders<KnowledgePath>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending(p => p.SubjectId).Ascending(p => p.PathStatus),
                new CreateIndexOptions { Name = "ix_knowledge_paths_tenant_subject_status" }));
            knowledgePaths.Indexes.CreateOne(new CreateIndexModel<KnowledgePath>(
                Builders<KnowledgePath>.IndexKeys
                    .Ascending(p => p.TenantId).Ascending("Steps.ContentId"),
                new CreateIndexOptions { Name = "ix_knowledge_paths_tenant_step_content" }));

            // MOD-0162 FU05 — content_engagement_journeys (one collection; stages embedded). EffectiveFrom /
            // EffectiveTo / ArchivedAt / StageSetFrozenAt are DateTimeOffset (BSON array) and are deliberately NOT index
            // keys and never sorted together (parallel-array trap); in-array StageOrder/StageCode uniqueness cannot be
            // an index (the handler is the only defence) and (JourneyCode, JourneyVersion) uniqueness is enforced in the
            // create handler (an archived code is reusable), so no partial $ne filter that would crash-loop the service.
            var journeys = database.GetCollection<ContentEngagementJourney>(
                ContentEngagementJourneyRepository.CollectionName);
            journeys.Indexes.CreateOne(new CreateIndexModel<ContentEngagementJourney>(
                Builders<ContentEngagementJourney>.IndexKeys
                    .Ascending(j => j.TenantId).Ascending(j => j.JourneyCode).Ascending(j => j.JourneyVersion),
                new CreateIndexOptions { Name = "ix_content_engagement_journeys_tenant_code_version" }));
            journeys.Indexes.CreateOne(new CreateIndexModel<ContentEngagementJourney>(
                Builders<ContentEngagementJourney>.IndexKeys
                    .Ascending(j => j.TenantId).Ascending(j => j.SubjectId).Ascending(j => j.JourneyStatus),
                new CreateIndexOptions { Name = "ix_content_engagement_journeys_tenant_subject_status" }));
            journeys.Indexes.CreateOne(new CreateIndexModel<ContentEngagementJourney>(
                Builders<ContentEngagementJourney>.IndexKeys
                    .Ascending(j => j.TenantId).Ascending("Stages.RecommendedKnowledgePathId"),
                new CreateIndexOptions { Name = "ix_content_engagement_journeys_tenant_stage_path" }));

            // MOD-0167 FU02 - segments (one collection; criteria embedded) + target_customers. EffectiveFrom /
            // EffectiveTo / CriteriaFrozenAt / ActivatedAt / ArchivedAt are DateTimeOffset (BSON array) and are
            // deliberately NOT index keys and never sorted together (the parallel-array trap). SegmentCode uniqueness
            // is enforced in the create handler (an archived code is reusable), so no partial index needs a $ne filter,
            // which crash-loops the service at startup.
            var segments = database.GetCollection<Segment>(SegmentRepository.CollectionName);
            segments.Indexes.CreateOne(new CreateIndexModel<Segment>(
                Builders<Segment>.IndexKeys.Ascending(s => s.TenantId).Ascending(s => s.SegmentCode),
                new CreateIndexOptions { Name = "ix_segments_tenant_code" }));
            segments.Indexes.CreateOne(new CreateIndexModel<Segment>(
                Builders<Segment>.IndexKeys
                    .Ascending(s => s.TenantId).Ascending(s => s.SegmentStatus).Ascending(s => s.SegmentType),
                new CreateIndexOptions { Name = "ix_segments_tenant_status_type" }));
            segments.Indexes.CreateOne(new CreateIndexModel<Segment>(
                Builders<Segment>.IndexKeys
                    .Ascending(s => s.TenantId).Ascending(s => s.VersionLineageId).Ascending(s => s.SegmentVersion),
                new CreateIndexOptions { Name = "ix_segments_tenant_lineage_version" }));
            segments.Indexes.CreateOne(new CreateIndexModel<Segment>(
                Builders<Segment>.IndexKeys.Ascending(s => s.TenantId).Ascending(s => s.SubjectType),
                new CreateIndexOptions { Name = "ix_segments_tenant_subject_type" }));

            var targetCustomers =
                database.GetCollection<TargetCustomer>(TargetCustomerRepository.CollectionName);
            targetCustomers.Indexes.CreateOne(new CreateIndexModel<TargetCustomer>(
                Builders<TargetCustomer>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.SegmentId)
                    .Ascending(t => t.SubjectType).Ascending(t => t.SubjectId),
                new CreateIndexOptions { Name = "ix_target_customers_tenant_segment_subject" }));
            // The reverse question: which segments has this person been added to (or excluded from) by hand?
            targetCustomers.Indexes.CreateOne(new CreateIndexModel<TargetCustomer>(
                Builders<TargetCustomer>.IndexKeys.Ascending(t => t.TenantId).Ascending(t => t.SubjectId),
                new CreateIndexOptions { Name = "ix_target_customers_tenant_subject" }));

            // MOD-0167 FU04 - strategy_templates (one collection; all four binding lists embedded). EffectiveFrom /
            // EffectiveTo / BindingsFrozenAt / ActivatedAt / ArchivedAt are DateTimeOffset (BSON array) and are
            // deliberately NOT index keys and never sorted together. TemplateCode uniqueness is enforced in the create
            // handler (an archived code is reusable), so no partial index needs a $ne filter.
            var strategyTemplates =
                database.GetCollection<Domain.Entities.StrategyTemplate>(StrategyTemplateRepository.CollectionName);
            strategyTemplates.Indexes.CreateOne(new CreateIndexModel<Domain.Entities.StrategyTemplate>(
                Builders<Domain.Entities.StrategyTemplate>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.TemplateCode),
                new CreateIndexOptions { Name = "ix_strategy_templates_tenant_code" }));
            strategyTemplates.Indexes.CreateOne(new CreateIndexModel<Domain.Entities.StrategyTemplate>(
                Builders<Domain.Entities.StrategyTemplate>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.TemplateStatus).Ascending(t => t.SubjectType),
                new CreateIndexOptions { Name = "ix_strategy_templates_tenant_status_subject" }));
            strategyTemplates.Indexes.CreateOne(new CreateIndexModel<Domain.Entities.StrategyTemplate>(
                Builders<Domain.Entities.StrategyTemplate>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending(t => t.VersionLineageId).Ascending(t => t.TemplateVersion),
                new CreateIndexOptions { Name = "ix_strategy_templates_tenant_lineage_version" }));
            // The reverse question: which plays bind this segment? (multikey over the embedded binding list)
            strategyTemplates.Indexes.CreateOne(new CreateIndexModel<Domain.Entities.StrategyTemplate>(
                Builders<Domain.Entities.StrategyTemplate>.IndexKeys
                    .Ascending(t => t.TenantId).Ascending("SegmentBindings.SegmentId"),
                new CreateIndexOptions { Name = "ix_strategy_templates_tenant_segment_binding" }));

            // MOD-0155 FU01 - planned_visits. PlannedDate is a DateOnly stored as a "yyyy-MM-dd" STRING, so it is a
            // plain scalar here and safe to index/sort - which is the whole reason the field is a DateOnly and not a
            // DateTimeOffset (a co-sorted DateTimeOffset pair is the parallel-arrays 500). ArchivedAt/CreatedAt/UpdatedAt
            // are DateTimeOffset (BSON arrays) and are deliberately NEVER index keys. VisitCode uniqueness is enforced in
            // the create handler (an archived code is reusable, which a partial filter cannot express, and $ne in a
            // partial-index filter crash-loops the service at startup), so this is a plain lookup index rather than a
            // unique one.
            var plannedVisits = database.GetCollection<PlannedVisit>(PlannedVisitRepository.CollectionName);
            plannedVisits.Indexes.CreateOne(new CreateIndexModel<PlannedVisit>(
                Builders<PlannedVisit>.IndexKeys.Ascending(v => v.TenantId).Ascending(v => v.VisitCode),
                new CreateIndexOptions { Name = "ix_planned_visits_tenant_code" }));
            plannedVisits.Indexes.CreateOne(new CreateIndexModel<PlannedVisit>(
                Builders<PlannedVisit>.IndexKeys
                    .Ascending(v => v.TenantId).Ascending(v => v.PlannedDate).Ascending(v => v.PlanStatus),
                new CreateIndexOptions { Name = "ix_planned_visits_tenant_date_status" }));
            plannedVisits.Indexes.CreateOne(new CreateIndexModel<PlannedVisit>(
                Builders<PlannedVisit>.IndexKeys
                    .Ascending(v => v.TenantId).Ascending("Resource.ResourceId").Ascending(v => v.PlannedDate),
                new CreateIndexOptions { Name = "ix_planned_visits_tenant_resource_date" }));
            plannedVisits.Indexes.CreateOne(new CreateIndexModel<PlannedVisit>(
                Builders<PlannedVisit>.IndexKeys
                    .Ascending(v => v.TenantId).Ascending(v => v.TargetType).Ascending(v => v.TargetId),
                new CreateIndexOptions { Name = "ix_planned_visits_tenant_target" }));

            // MOD-0155 FU05 - planning_sessions. Plain lookup indexes only; NO $ne partial filter (a $ne in a partial
            // index filter crash-loops the service at startup). CreatedAt is a DateTimeOffset (BSON array) and is
            // deliberately never an index key. The session-for-rep-in-period lookup and the status filter are the two
            // access paths the console needs.
            var planningSessions = database.GetCollection<PlanningSession>(PlanningSessionRepository.CollectionName);
            planningSessions.Indexes.CreateOne(new CreateIndexModel<PlanningSession>(
                Builders<PlanningSession>.IndexKeys
                    .Ascending(s => s.TenantId).Ascending(s => s.CyclePeriodId).Ascending(s => s.ResourceId),
                new CreateIndexOptions { Name = "ix_planning_sessions_tenant_period_resource" }));
            planningSessions.Indexes.CreateOne(new CreateIndexModel<PlanningSession>(
                Builders<PlanningSession>.IndexKeys.Ascending(s => s.TenantId).Ascending(s => s.Status),
                new CreateIndexOptions { Name = "ix_planning_sessions_tenant_status" }));

            // MOD-0155 FU02 - visit_reports. Plain lookup indexes only; NO $ne partial filter (a $ne in a partial index
            // filter crash-loops the service at startup). The report-for-a-visit lookup (TenantId+PlannedVisitId) + the
            // status filter are the two access paths; ContentActuals.StageIndex is indexed for the "last completed stage
            // per doctor" §4.4 read. ExecutedAt/SubmittedAt/AmendedAt/CreatedAt/UpdatedAt are DateTimeOffset (BSON arrays)
            // and are deliberately NEVER index keys and never co-sorted (the CRM parallel-arrays 500).
            var visitReports = database.GetCollection<VisitReport>(VisitReportRepository.CollectionName);
            visitReports.Indexes.CreateOne(new CreateIndexModel<VisitReport>(
                Builders<VisitReport>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.PlannedVisitId),
                new CreateIndexOptions { Name = "ix_visit_reports_tenant_planned_visit" }));
            visitReports.Indexes.CreateOne(new CreateIndexModel<VisitReport>(
                Builders<VisitReport>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.ReportStatus),
                new CreateIndexOptions { Name = "ix_visit_reports_tenant_status" }));
            visitReports.Indexes.CreateOne(new CreateIndexModel<VisitReport>(
                Builders<VisitReport>.IndexKeys
                    .Ascending(r => r.TenantId).Ascending(r => r.ReportedByResourceId)
                    .Ascending("ContentActuals.StageIndex"),
                new CreateIndexOptions { Name = "ix_visit_reports_tenant_resource_stage" }));
        }
        catch (MongoException)
        {
            // Index creation is best-effort at startup; a running Mongo is not required for DI wiring/build.
        }
    }

    /// <summary>Drops an index by name if present (ignores "not found"). Used to migrate the historical-lifecycle
    /// partial unique indexes from an IsDeleted-only filter to an IsDeleted+Status="active" filter on startup.</summary>
    private static void DropIndexIfExists<T>(IMongoCollection<T> collection, string name)
    {
        try
        {
            collection.Indexes.DropOne(name);
        }
        catch (MongoException)
        {
            // Index not present (fresh DB) — nothing to migrate.
        }
    }
}
