using System.Net;
using System.Reflection;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX2 — the store rules, on this host's throwaway mongod (never the shared test database:
/// BL-527). A race is made real: a second client writes at the moment the repository sends its write, so only the
/// write's own condition (or its transaction) can refuse it.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private const string Operations = "mdm_first_gsku_identity_workflow_operations";

    // Item 10, the edit side: a submit's reservation commits between the edit's read and its write. The edit's
    // transaction conflicts, reads again, sees the submit, and is refused — the draft keeps what was submitted.
    [Fact]
    public async Task F10_An_edit_racing_a_submit_reservation_is_refused_and_the_draft_keeps_what_was_submitted()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var plain = host.Database;
        var raced = false;
        var hooked = Hooked("findAndModify", "mdm_gskus", () =>
        {
            raced = true;
            using var session = plain.Client.StartSession();
            session.StartTransaction();
            plain.GetCollection<BsonDocument>(Operations).InsertOne(session, new BsonDocument
            {
                { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard) },
                { "OperationId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "GskuId", new BsonBinaryData(gsku, GuidRepresentation.Standard) },
                { "Checkpoint", (int)FirstGskuIdentityWorkflowCheckpoint.Prepared },
                { "ExpectedGskuVersion", version },
                { "IsDeleted", false },
                { "CreatedAtUtcTicksV1", DateTimeOffset.UtcNow.UtcTicks }
            });
            plain.GetCollection<Gsku>("mdm_gskus").UpdateOne(session, x => x.Id == gsku,
                Builders<Gsku>.Update.Inc(x => x.IdentitySubmitReservations, 1));
            session.CommitTransaction();
        });
        var repository = new GskuRepository(hooked, Tenant());
        var draft = (await repository.GetByIdAsync(gsku))!;
        draft.PackQuantity = 9;
        draft.AuditIntents.Add(new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateType = AuditAggregateType.Gsku, AggregateId = gsku,
            Operation = ProductAuditOperation.GskuDraftUpdated, IdempotencyKey = Guid.NewGuid().ToString("D"),
            EvidenceHash = "race", PreVersion = version, PostVersion = version + 1
        });

        var result = await repository.UpdateDraftAsync(draft, version);

        Assert.True(raced);
        Assert.False(result.Succeeded);
        Assert.Equal(GskuRepository.SubmitInFlight, result.ErrorCode);
        Assert.Equal(1m, (await DetailAsync(gsku)).Quantity);
    }

    // Item 10, the submit side: an edit commits between the submit's read and its reservation → the reservation is
    // refused (the draft is not what was read) and no approval is started on facts the record no longer has.
    [Fact]
    public async Task F10_A_submit_reservation_racing_an_edit_is_refused()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var plain = host.Database;
        var hooked = Hooked("insert", Operations, () =>
            plain.GetCollection<Gsku>("mdm_gskus").UpdateOne(x => x.Id == gsku,
                Builders<Gsku>.Update.Inc(x => x.Version, 1).Set(x => x.PackQuantity, 2m)));
        var stored = await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var revision = await plain.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == stored.ProductDefinitionRevisionId).SingleAsync();

        var reserved = await new FirstGskuIdentityWorkflowOperationRepository(hooked, Tenant())
            .ReserveAsync(ReservableOperation(stored, revision, version));

        Assert.False(reserved.Succeeded);
        Assert.Equal("FIRST_GSKU_IDENTITY_WORKFLOW_DRAFT_CHANGED", reserved.ErrorCode);
        Assert.False(await plain.GetCollection<BsonDocument>(Operations)
            .Find(new BsonDocument("GskuId", new BsonBinaryData(gsku, GuidRepresentation.Standard))).AnyAsync());
    }

    // GR:864 / GR:866 — giving a lifecycle lock back is conditioned on the version AND on this very operation's
    // binding; a writer that got in after the read (a new version, another operation's lock) keeps what it wrote.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GR_A_lock_release_racing_another_writer_writes_nothing(bool versionMoved)
    {
        var approved = await ApprovedGskuAsync();
        var operationId = Guid.NewGuid();
        var binding = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Correction, operationId, approved.Version);
        var plain = host.Database;
        await plain.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == approved.Id,
            Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, binding).Inc(x => x.Version, 1));
        var other = new GskuActiveLifecycleOperationBinding(GskuLifecycleOperationKind.Retirement, Guid.NewGuid(), approved.Version);
        var hooked = Hooked("findAndModify", "mdm_gskus", () =>
            plain.GetCollection<Gsku>("mdm_gskus").UpdateOne(x => x.Id == approved.Id, versionMoved
                ? Builders<Gsku>.Update.Inc(x => x.Version, 1)
                : Builders<Gsku>.Update.Set(x => x.ActiveLifecycleOperation, other)));
        var gsku = await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == approved.Id).SingleAsync();
        var catalog = Selection("uom", "C62");
        var audit = GskuCorrectionAuditIntentFactory.Create(gsku, approved.Version + 1, operationId, Guid.NewGuid(),
            ProductAuditOperation.GskuCorrectionManualReconciliationRequired, 1, "C62",
            Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"), catalog, DateTimeOffset.UtcNow);

        var result = await new GskuRepository(hooked, Tenant())
            .ReleaseLifecycleOperationAsync(approved.Id, approved.Version + 1, binding, audit);

        Assert.False(result.Succeeded);
        var after = await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == approved.Id).SingleAsync();
        Assert.NotNull(after.ActiveLifecycleOperation);
        Assert.DoesNotContain(after.AuditIntents, x => x.IdempotencyKey == audit.IdempotencyKey);
    }

    // IDR:434 / IDR:454 — the identity operation store is tenant-bound: another tenant cannot give back this tenant's
    // lease, nor read where this tenant's submit stands.
    [Fact]
    public async Task IDR_Another_tenant_neither_releases_nor_sees_this_tenants_submit()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        host.Engine.NextStartFails = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome.Timeout;
        host.Engine.LookupFailure = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome.Timeout;
        try { await SubmitAsync(gsku, version); }
        finally { host.Engine.LookupFailure = null; }
        var collection = host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(Operations);
        var operation = await collection.Find(x => x.GskuId == gsku).SingleAsync();
        await collection.UpdateOneAsync(x => x.Id == operation.Id, Builders<FirstGskuIdentityWorkflowOperation>.Update
            .Set(x => x.LeaseOwner, "owner").Set(x => x.LeaseUntilUtcTicksV1, 42L));
        var stranger = new FirstGskuIdentityWorkflowOperationRepository(host.Database, Tenant(Guid.NewGuid()));

        await stranger.ReleaseIfOwnedAsync(operation.OperationId, "owner", 42L);
        var state = await stranger.GetSubmitStateAsync(gsku, version);

        Assert.Equal("owner", (await collection.Find(x => x.Id == operation.Id).SingleAsync()).LeaseOwner);
        Assert.Equal(FirstGskuIdentitySubmitStateKind.None, state.Kind);
        Assert.Equal(FirstGskuIdentitySubmitStateKind.InFlight,
            (await new FirstGskuIdentityWorkflowOperationRepository(host.Database, Tenant()).GetSubmitStateAsync(gsku, version)).Kind);
    }

    // IDR:523 — "not applicable" returns the pair to draft only from pending: a GSKU that is no longer pending (at
    // the same version) is not written. Called at the store, past the processor's own read, so only the store's
    // condition can refuse it.
    [Fact]
    public async Task IDR_Not_applicable_writes_nothing_on_a_gsku_that_is_no_longer_pending()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        var operations = host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(Operations);
        var operation = await operations.Find(x => x.GskuId == gsku).SingleAsync();
        await operations.UpdateOneAsync(x => x.Id == operation.Id, Builders<FirstGskuIdentityWorkflowOperation>.Update
            .Set(x => x.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
            .Set(x => x.DecisionKind, ProductIdentityDecisionKind.Approved)
            .Set(x => x.DecisionActorSubjectId, host.Approver));
        await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
            Builders<Gsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft));
        operation = await operations.Find(x => x.Id == operation.Id).SingleAsync();
        var repository = new FirstGskuIdentityWorkflowOperationRepository(host.Database, Tenant());
        var now = DateTimeOffset.UtcNow;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [FirstGskuIdentityWorkflowCheckpoint.DecisionObserved], "store-test", now.UtcTicks, now.AddMinutes(1).UtcTicks));
        Assert.NotNull(claim);
        var revision = await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == operation.ProductDefinitionRevisionId).SingleAsync();
        var stored = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var audit = typeof(FirstGskuIdentityWorkflowProcessor).GetMethod("CreateNotApplicableAudit",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        LocalAuditIntent Audit(EntityBase aggregate, AuditAggregateType type, ProductAuditOperation kind, int expected) =>
            (LocalAuditIntent)audit.Invoke(null, [aggregate, type, kind, expected, operation,
                "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED", now, null])!;

        var result = await repository.ApplyDecisionNotApplicableAsync(claim!, operation, "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED",
            Audit(revision, AuditAggregateType.ProductDefinitionRevision,
                ProductAuditOperation.ProductDefinitionRevisionIdentityDecisionNotApplicable, operation.ExpectedRevisionVersion + 1),
            Audit(stored, AuditAggregateType.Gsku, ProductAuditOperation.GskuIdentityDecisionNotApplicable,
                operation.ExpectedGskuVersion + 1),
            now.UtcTicks);

        Assert.False(result.Succeeded);
        var after = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        Assert.DoesNotContain(after.AuditIntents, x => x.Operation == ProductAuditOperation.GskuIdentityDecisionNotApplicable);
        Assert.Equal(stored.Version, after.Version);
    }

    // COR:274 — at the moment a correction is applied, a reference service that is down (504) or refuses this service
    // (401 / 403) is waited for: the correction is not ended and the GSKU keeps its lock until the answer comes.
    [Theory]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task COR_A_reference_outage_at_apply_time_is_waited_for(HttpStatusCode status)
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 7m)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        host.PlatformReference.FailWith = status;
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await host.RunAsync<GskuCorrectionRecoveryRunner>(runner => runner.RunCycleAsync());
                await Task.Delay(1100);
            }
            var waiting = await DetailAsync(approved.Id);
            Assert.DoesNotContain("REQUEST_CORRECTION", waiting.Actions);
            Assert.Equal(1m, waiting.Quantity);
        }
        finally
        {
            host.PlatformReference.FailWith = null;
        }
        var applied = await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Quantity == 7m);
        Assert.Contains("REQUEST_CORRECTION", applied.Actions);
    }

    // Item 11 (B12's remainder): the correction's decision was written, then the step was cut off before it was
    // recorded; the catalog version moved before the repeat. The repeat uses what was written (no new resolution) and
    // completes — FIX1 re-resolved, the evidence changed, and the repeat conflicted with its own write for ever.
    [Fact]
    public async Task F11_A_correction_repeat_after_its_decision_was_written_completes_despite_a_new_catalog_version()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 8m)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        var operation = await host.Database.GetCollection<GskuCorrectionWorkflowOperation>(
                GskuCorrectionWorkflowOperationRepository.CollectionName)
            .Find(x => x.GskuId == approved.Id).SortByDescending(x => x.CreatedAtUtcTicksV1).FirstAsync();
        var references = new VersionedReferences();
        var processor = CorrectionProcessor(references, crashAfterApply: true);
        var execution = new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<IOException>(() => RecoverUntilApplyAsync(processor, operation.OperationId, execution));
        references.Version = 2;
        var operations = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
        await Task.Delay(TimeSpan.FromSeconds(10.5)); // the cut-off step's lease runs out
        var repeat = await CorrectionProcessor(references, crashAfterApply: false).RecoverAsync(
            (await operations.GetByOperationIdAsync(operation.OperationId))!, "repeat", execution, default);

        Assert.True(repeat.Succeeded, repeat.ErrorCode);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.Completed, repeat.Operation!.Checkpoint);
        Assert.Equal(8m, (await DetailAsync(approved.Id)).Quantity);
    }

    private async Task RecoverUntilApplyAsync(GskuCorrectionWorkflowProcessor processor, Guid operationId,
        GskuCorrectionExecutionConfiguration execution)
    {
        var operations = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await processor.RecoverAsync((await operations.GetByOperationIdAsync(operationId))!, "first", execution, default);
            await Task.Delay(1100);
        }
    }

    private GskuCorrectionWorkflowProcessor CorrectionProcessor(VersionedReferences references, bool crashAfterApply)
    {
        var tenant = Tenant();
        var gskus = new GskuRepository(host.Database, tenant);
        IGskuRepository store = gskus;
        if (crashAfterApply)
        {
            var proxy = DispatchProxy.Create<IGskuRepository, CrashAfterApply>();
            ((CrashAfterApply)(object)proxy).Inner = gskus;
            store = proxy;
        }
        return new GskuCorrectionWorkflowProcessor(
            new GskuCorrectionWorkflowOperationRepository(host.Database, tenant), store,
            new ProductDefinitionRevisionRepository(host.Database, tenant), host.Engine, references,
            new GskuCorrectionWorkflowStartRequestFactory(new(null, "GSKU-CORRECTION", [host.Approver], "GSKU_CORRECTION",
                false, false, null, null, "GSKU-IDENTITY"), TimeProvider.System),
            TimeProvider.System);
    }

    public class CrashAfterApply : DispatchProxy
    {
        public IGskuRepository Inner { get; set; } = null!;
        private bool _crashed;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var result = method!.Invoke(Inner, args);
            if (method.Name == nameof(IGskuRepository.ApplyCorrectionDecisionAsync) && !_crashed)
            {
                _crashed = true;
                return Fail((Task<GskuCorrectionWriteResult>)result!);
            }
            return result;
        }

        private static async Task<GskuCorrectionWriteResult> Fail(Task<GskuCorrectionWriteResult> written)
        {
            await written; // the write lands …
            throw new IOException("cut off after the write"); // … and the step never learns it
        }
    }

    private sealed class VersionedReferences : IWorkflowVerifiedGskuReferenceResolver
    {
        public int Version { get; set; } = 1;

        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(Guid tenantId, string pack, string uom,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(VerifiedGskuReferenceResolveResult.Success([
                new VerifiedGskuReferenceSelection("pack-applicability", pack,
                    new Guid($"c0000000-0000-0000-0000-{Version:D12}"), Version, "LATEST", DateTimeOffset.UtcNow, false, true),
                new VerifiedGskuReferenceSelection("uom", uom,
                    new Guid($"d0000000-0000-0000-0000-{Version:D12}"), Version, "LATEST", DateTimeOffset.UtcNow, false, true)]));
    }

    private TenantContext Tenant(Guid? tenant = null)
    {
        var context = new TenantContext();
        context.SetTenant(tenant ?? _tenant);
        return context;
    }

    private IMongoDatabase Hooked(string command, string collection, Action racer)
    {
        // The host's own client settings (its Guid representation included), plus the hook.
        var settings = host.Database.Client.Settings.Clone();
        var armed = 1;
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(started =>
        {
            if (started.CommandName == command
                && started.Command.GetValue(command, "").AsString == collection
                && Interlocked.Exchange(ref armed, 0) == 1)
                racer();
        });
        return new MongoClient(settings).GetDatabase(MdmHttpHost.DatabaseName);
    }

    private static ReferenceCatalogSelection Selection(string set, string value) => new()
    {
        SetCode = set, ValueCode = value, CatalogVersionId = Guid.NewGuid(), CatalogVersionNumber = 1,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest, ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private FirstGskuIdentityWorkflowOperation ReservableOperation(Gsku gsku, ProductDefinitionRevision revision, int version)
    {
        var id = Guid.NewGuid();
        return new FirstGskuIdentityWorkflowOperation
        {
            OperationId = id, ProductDefinitionRevisionId = revision.Id, GskuId = gsku.Id,
            GlobalProductId = revision.GlobalProductId, CreationCommandId = gsku.CreationCommandId,
            ExpectedRevisionVersion = revision.Version, ExpectedGskuVersion = version,
            PackQuantity = gsku.PackQuantity, PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackUomCode = "C62",
            PackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
            PackUomSelection = Selection("uom", "C62"), MakerSubjectId = _maker, WorkflowTemplateCode = "GSKU-IDENTITY",
            CandidatePrincipalIds = [host.Approver], ReasonCode = "FIRST_GSKU_IDENTITY_APPROVAL",
            ObjectType = "gsku", ObjectId = gsku.Id.ToString("D"), ObjectRef = gsku.CanonicalCode,
            StartIdempotencyKey = $"race:{id:D}", OperationFingerprint = new string('f', 64)
        };
    }
}
