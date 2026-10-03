using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 — the four GskuRepository rules the acceptance review found untested (GR:378, GR:467, GR:599,
/// GR:624 at ec6790398). Each is the condition in the single conditional write that holds when another writer gets in
/// AFTER the method's own reads: the competing write is made by a second client at the moment the repository's
/// findAndModify is sent, so only the write's filter can refuse it. Remove the condition and the write lands.
/// </summary>
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GskuRepositoryRaceMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _plain = null!;
    private IMongoDatabase _hooked = null!;
    private Action? _racer;

    public async Task InitializeAsync()
    {
        _plain = Client(null).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _plain.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _hooked = Client(started =>
        {
            if (started.CommandName != "findAndModify"
                || started.Command.GetValue("findAndModify", "").AsString != "mdm_gskus") return;
            var racer = Interlocked.Exchange(ref _racer, null);
            racer?.Invoke();
        }).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
    }

    public async Task DisposeAsync() =>
        await _plain.GetCollection<Gsku>("mdm_gskus").DeleteManyAsync(x => x.TenantId == _tenantId);

    // GR:378 — a draft edit racing a submit: the submit moved the GSKU on (same version), the edit must not land.
    [Fact]
    public async Task GR_A_draft_edit_racing_a_submit_writes_nothing()
    {
        var gsku = await SeedAsync(ProductIdentityLifecycleStatus.Draft, 1);
        Race(Builders<Gsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval), gsku.Id);
        gsku.PackQuantity = 9;
        gsku.AuditIntents.Add(Intent(gsku, ProductAuditOperation.GskuDraftUpdated, 1, Guid.NewGuid().ToString("D")));

        var result = await Repository().UpdateDraftAsync(gsku, 1);

        Assert.False(result.Succeeded);
        var stored = await StoredAsync(gsku.Id);
        Assert.Equal(1m, stored.PackQuantity);
        Assert.Empty(stored.AuditIntents);
    }

    // GR:467 — a child admission racing the retirement fence or a lifecycle lock is refused.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GR_A_child_admission_racing_a_retirement_fence_or_a_lifecycle_lock_is_refused(bool fence)
    {
        var gsku = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, 2);
        Race(fence
            ? Builders<Gsku>.Update.Set(x => x.RetirementOperationId, Guid.NewGuid())
            : Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation,
                new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, Guid.NewGuid(), 2)), gsku.Id);
        var fingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(gsku.Id, GskuChildIdentityKind.Lsku, "lsku-1", "TR");

        var result = await Repository().AcquireChildCreationAdmissionAsync(gsku.Id, GskuChildIdentityKind.Lsku, "lsku-1",
            fingerprint, DateTimeOffset.UtcNow);

        Assert.False(result.Succeeded);
        Assert.Empty((await StoredAsync(gsku.Id)).ChildCreationAdmissions);
    }

    // GR:599 — retirement racing a child admission that got in after the blocker check: the pair is not retired.
    [Fact]
    public async Task GR_Retirement_racing_a_child_admission_writes_nothing()
    {
        var gsku = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, 2);
        var operationId = Guid.NewGuid();
        var fenced = await Repository().CloseChildAdmissionFenceAsync(gsku.Id, 2, operationId, "retire-fingerprint");
        Assert.True(fenced.Succeeded, fenced.ErrorCode);
        Race(Builders<Gsku>.Update.Push(x => x.ChildCreationAdmissions, new GskuChildCreationAdmission
        {
            ChildKind = GskuChildIdentityKind.Lsku, CreationCommandId = "late-child",
            RequestFingerprint = new string('d', 64), AcquiredAtUtc = DateTimeOffset.UtcNow
        }), gsku.Id);

        var result = await Repository().RetireIdentityAsync(gsku.Id, fenced.Aggregate!.Version, operationId,
            "retire-fingerprint", Intent(fenced.Aggregate, ProductAuditOperation.GskuIdentityRetired,
                fenced.Aggregate.Version, "retire-gsku"));

        Assert.False(result.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await StoredAsync(gsku.Id)).LifecycleStatus);
    }

    // GR:624 — the lifecycle lock: a retirement request racing another operation's lock does not take it over.
    [Fact]
    public async Task GR_A_retirement_request_racing_another_lock_is_refused_and_the_other_lock_stays()
    {
        var gsku = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, 2);
        var other = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, Guid.NewGuid(), 2);
        Race(Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, other), gsku.Id);
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Retirement, operationId, 2);

        var result = await Repository().AcquireRetirementRequestAsync(gsku.Id, 2, binding,
            GskuRetirementRequestAuditIntentFactory.Create(gsku, 2, operationId, Guid.NewGuid(),
                ProductAuditOperation.GskuRetirementRequested, "retire obsolete presentation", DateTimeOffset.UtcNow));

        Assert.False(result.Succeeded);
        Assert.Equal("GSKU_LIFECYCLE_OPERATION_ACTIVE", result.ErrorCode);
        var stored = await StoredAsync(gsku.Id);
        Assert.Equal(other.OperationId, stored.ActiveLifecycleOperation!.OperationId);
        Assert.Empty(stored.AuditIntents);
    }

    // B10: once the audit intent of a correction was delivered and compacted to a receipt, the same request is still
    // recognised as the same request — from the receipt — and is answered as a replay, not as a conflict.
    [Fact]
    public async Task B10_A_correction_whose_intent_became_a_receipt_is_still_an_exact_replay()
    {
        var gsku = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, 2);
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, operationId, 2);
        var requested = CorrectionIntent(gsku, operationId, DateTimeOffset.UtcNow);
        Assert.True((await Repository().AcquireCorrectionAsync(gsku.Id, 2, binding, requested)).Succeeded);
        await _plain.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == gsku.Id,
            Builders<Gsku>.Update.PullFilter(x => x.AuditIntents, x => x.IdempotencyKey == requested.IdempotencyKey)
                .Push(x => x.AuditIntentReceipts, new LocalAuditIntentReceipt
                {
                    SourceService = "mdm", IntentId = requested.IntentId, TenantId = _tenantId,
                    IdempotencyKey = requested.IdempotencyKey, CentralAcknowledgement = "ack",
                    CentralIdempotencyKey = requested.IdempotencyKey, ContractVersion = "1",
                    AcknowledgedAt = DateTimeOffset.UtcNow, DeliveredAt = DateTimeOffset.UtcNow,
                    CompactedAt = DateTimeOffset.UtcNow, CompactReceiptReference = "receipt-1",
                    EvidenceHash = requested.EvidenceHash
                }));

        var replay = await Repository().AcquireCorrectionAsync(gsku.Id, 2, binding, requested);

        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.True(replay.IsReplay);
        Assert.Equal(3, (await StoredAsync(gsku.Id)).Version);
    }

    // B10: the 33rd audited write on one GSKU is refused and says why — not a silent "conflict".
    [Fact]
    public async Task B10_The_write_over_the_audit_intent_capacity_is_refused_as_capacity_reached()
    {
        var gsku = await SeedAsync(ProductIdentityLifecycleStatus.IdentityApproved, 2);
        var filler = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate)
            .Select(i => Intent(gsku, ProductAuditOperation.GskuDraftUpdated, i, $"filler-{i}-{Guid.NewGuid():N}")).ToList();
        await _plain.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.TenantId == _tenantId && x.Id == gsku.Id,
            Builders<Gsku>.Update.Set(x => x.AuditIntents, filler));
        var operationId = Guid.NewGuid();

        var result = await Repository().AcquireCorrectionAsync(gsku.Id, 2,
            new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, operationId, 2),
            CorrectionIntent(gsku, operationId, DateTimeOffset.UtcNow));

        Assert.False(result.Succeeded);
        Assert.Equal(GskuRepository.AuditCapacityReached, result.ErrorCode);
        Assert.Null((await StoredAsync(gsku.Id)).ActiveLifecycleOperation);
    }

    // B12: the evidence hash is what was decided, not when the reference was read: a repeat that re-resolves the
    // same catalog version a moment later yields the same hash (before, its replay was refused as a conflict).
    [Fact]
    public void B12_The_correction_evidence_does_not_depend_on_when_the_reference_was_read()
    {
        var gsku = new Gsku { Id = Guid.NewGuid(), TenantId = _tenantId, Version = 2 };
        var operationId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var catalog = Guid.NewGuid();
        LocalAuditIntent At(DateTimeOffset read, int version = 1) => GskuCorrectionAuditIntentFactory.Create(gsku, 2,
            operationId, actor, ProductAuditOperation.GskuCorrectionRequested, 12, "KGM",
            Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES", catalog, version, read),
            Selection("uom", "KGM", catalog, version, read), DateTimeOffset.UtcNow);

        var first = At(DateTimeOffset.UtcNow.AddMinutes(-5));
        var reread = At(DateTimeOffset.UtcNow);
        var newVersion = At(DateTimeOffset.UtcNow, version: 2);

        Assert.Equal(first.EvidenceHash, reread.EvidenceHash);
        Assert.NotEqual(first.EvidenceHash, newVersion.EvidenceHash);
    }

    private static ReferenceCatalogSelection Selection(string set, string value, Guid catalog, int version,
        DateTimeOffset read) => new()
    {
        SetCode = set, ValueCode = value, CatalogVersionId = catalog, CatalogVersionNumber = version,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest, ResolvedAtUtc = read.ToUniversalTime()
    };

    private LocalAuditIntent CorrectionIntent(Gsku gsku, Guid operationId, DateTimeOffset now)
    {
        var catalog = Guid.NewGuid();
        return GskuCorrectionAuditIntentFactory.Create(gsku, 2, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuCorrectionRequested, 12, "KGM",
            Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES", catalog, 1, now),
            Selection("uom", "KGM", catalog, 1, now), now.ToUniversalTime());
    }

    private void Race(UpdateDefinition<Gsku> competing, Guid gskuId) => _racer = () =>
        _plain.GetCollection<Gsku>("mdm_gskus").UpdateOne(x => x.TenantId == _tenantId && x.Id == gskuId, competing);

    private GskuRepository Repository() => new(_hooked, new Tenant(_tenantId));

    private async Task<Gsku> StoredAsync(Guid id) =>
        await _plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.TenantId == _tenantId && x.Id == id).SingleAsync();

    private async Task<Gsku> SeedAsync(ProductIdentityLifecycleStatus status, int version)
    {
        var id = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var gsku = new Gsku
        {
            Id = id, TenantId = _tenantId, ProductDefinitionRevisionId = revisionId,
            CanonicalCode = $"GS-{id:N}"[..20], CreationCommandId = Guid.NewGuid().ToString("D"),
            CodeReservationId = Guid.NewGuid(),
            PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackQuantity = 1, PackUomCode = "C62",
            LifecycleStatus = status,
            IdentityWorkflowBinding = status == ProductIdentityLifecycleStatus.IdentityApproved ? ApprovedBinding(id, revisionId) : null,
            CreatedAt = now, UpdatedAt = now, Version = version
        };
        await _plain.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        return gsku;
    }

    private static LocalAuditIntent Intent(Gsku aggregate, ProductAuditOperation operation, int version, string key) => new()
    {
        IntentId = Guid.NewGuid(), TenantId = aggregate.TenantId, AggregateType = AuditAggregateType.Gsku,
        AggregateId = aggregate.Id, PreVersion = version, PostVersion = version + 1,
        Operation = operation, ActorId = Guid.NewGuid().ToString("D"), CorrelationId = key,
        CausationId = key, CommandId = key, Sequence = version + 1,
        TimestampUtc = DateTimeOffset.UtcNow, EvidenceHash = $"hash-{key}", IdempotencyKey = key
    };

    private static FirstGskuIdentityWorkflowBinding ApprovedBinding(Guid gskuId, Guid revisionId)
    {
        var workflow = Guid.NewGuid();
        var template = Guid.NewGuid();
        var version = Guid.NewGuid();
        var task = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        return new()
        {
            WorkflowInstanceId = workflow, WorkflowTemplateId = template,
            WorkflowTemplateVersionId = version, ApprovalTaskId = task,
            AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
            ObjectType = "gsku", GskuId = gskuId, ProductDefinitionRevisionId = revisionId,
            ObjectRef = "GS-RACE", SubmitterSubjectId = Guid.NewGuid(),
            StartIdempotencyKey = "first-gsku", StartRequestFingerprint = new string('c', 64),
            SubmittedAtUtc = now,
            TerminalDecision = new()
            {
                Decision = ProductIdentityDecisionKind.Approved, WorkflowInstanceId = workflow,
                ApprovalTaskId = task, WorkflowTemplateId = template, WorkflowTemplateVersionId = version,
                ObjectType = "gsku", ObjectId = gskuId, ObjectRef = "GS-RACE",
                DecisionActorSubjectId = Guid.NewGuid(), DecisionAtUtc = now, TransitionSequence = 2,
                TaskStatus = "Approved", InstanceStatus = "Completed"
            }
        };
    }

    private static MongoClient Client(Action<CommandStartedEvent>? onStarted)
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        if (onStarted is not null)
        {
            settings.ApplicationName = "gsku-race-hook";
            settings.ClusterConfigurator = builder => builder.Subscribe(onStarted);
        }
        return new MongoClient(settings);
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
