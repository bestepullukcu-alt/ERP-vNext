using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU08 — controlled document lifecycle status engine tests. Tenant-aware in-memory fakes exercise the
/// transition matrix, MarkEffective guards, single-effective/supersession, operational-use rules and the transition
/// ledger. No approval/release-gate engine (FU09/FU10) is implemented — the extension-point fields are only read.
/// </summary>
public sealed class DocumentLifecycleStatusTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu08-corr-1";

    private static TransitionDocumentLifecycleInput To(string status, string? reason = null, DateTimeOffset? effectiveDate = null, Guid? replacement = null, int? expectedVersion = null) =>
        new(status, reason, EvidenceReference: null, Comment: null, effectiveDate, replacement, expectedVersion);

    [Fact]
    public async Task Draft_to_InReview_allowed()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft);

        var r = await f.Service.TransitionAsync(e.Id, To("InReview"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("InReview", r.Data!.CurrentStatus);
    }

    [Fact]
    public async Task InReview_to_Draft_allowed()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.InReview);

        var r = await f.Service.TransitionAsync(e.Id, To("Draft"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Draft", r.Data!.CurrentStatus);
    }

    [Fact]
    public async Task InReview_to_ApprovedPendingEffective_allowed()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.InReview);

        var r = await f.Service.TransitionAsync(e.Id, To("ApprovedPendingEffective"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.False(r.Data!.OperationalUseAllowed); // preparation only, not routine use
    }

    [Fact]
    public async Task ApprovedPendingEffective_to_Effective_allowed_when_uid_code_present()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Effective", r.Data!.CurrentStatus);
        Assert.True(r.Data.OperationalUseAllowed);
        Assert.NotNull(f.Register.Items.Single(x => x.Id == e.Id).EffectiveDate);
    }

    [Fact]
    public async Task MarkEffective_blocks_without_uid()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: null, code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.MissingIdentifier, r.ReasonCode);
    }

    [Fact]
    public async Task MarkEffective_blocks_without_code()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000001", code: null);

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.MissingIdentifier, r.ReasonCode);
    }

    [Fact]
    public async Task MarkEffective_blocks_retroactive_effective_date()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Effective", effectiveDate: DateTimeOffset.UtcNow.AddDays(-2)), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.RetroactiveEffectiveDate, r.ReasonCode);
    }

    [Fact]
    public async Task MarkEffective_blocks_from_Draft()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.InvalidTransition, r.ReasonCode);
    }

    [Fact]
    public async Task Effective_to_UnderRevision_allowed_and_operational_use_stays_true()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("UnderRevision"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("UnderRevision", r.Data!.CurrentStatus);
        Assert.True(r.Data.OperationalUseAllowed); // existing effective version remains in force (SOP §6.2)
    }

    // ── MOD-0029-FU08A — UnderRevision → Suspended / Retired amendment (SOP §6.2). ────────────────────────

    [Fact]
    public async Task UnderRevision_to_Suspended_allowed_and_operational_use_false()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.UnderRevision, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Suspended", reason: "safety risk during revision"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Suspended", r.Data!.CurrentStatus);
        Assert.False(r.Data.OperationalUseAllowed);
        Assert.Contains(f.Transitions.Items, x => x.FromStatus == ControlledDocumentLifecycleStatus.UnderRevision && x.ToStatus == ControlledDocumentLifecycleStatus.Suspended);
    }

    [Fact]
    public async Task UnderRevision_to_Retired_allowed_and_operational_use_false()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.UnderRevision, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Retired", reason: "withdrawn without replacement during revision"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Retired", r.Data!.CurrentStatus);
        Assert.False(r.Data.OperationalUseAllowed);
        Assert.Contains(f.Transitions.Items, x => x.FromStatus == ControlledDocumentLifecycleStatus.UnderRevision && x.ToStatus == ControlledDocumentLifecycleStatus.Retired);
    }

    [Fact]
    public async Task UnderRevision_to_Suspended_still_requires_reason()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.UnderRevision, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Suspended"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.ReasonRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Effective_to_Suspended_requires_reason()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var missing = await f.Service.TransitionAsync(e.Id, To("Suspended"), Corr, CancellationToken.None);
        Assert.False(missing.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.ReasonRequired, missing.ReasonCode);

        var ok = await f.Service.TransitionAsync(e.Id, To("Suspended", reason: "safety risk identified"), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Suspended", ok.Data!.CurrentStatus);
        Assert.False(ok.Data.OperationalUseAllowed);
    }

    [Fact]
    public async Task Suspended_to_Retired_allowed_and_Retired_is_terminal()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Suspended);

        var retire = await f.Service.TransitionAsync(e.Id, To("Retired", reason: "no valid replacement"), Corr, CancellationToken.None);
        Assert.True(retire.IsSuccessful);

        var backToEffective = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);
        Assert.False(backToEffective.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.InvalidTransition, backToEffective.ReasonCode);
    }

    [Fact]
    public async Task Superseded_is_terminal_blocks_effective()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Superseded);

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.InvalidTransition, r.ReasonCode);
    }

    [Fact]
    public async Task Suspended_to_Effective_reinstatement_is_blocked()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Suspended, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.InvalidTransition, r.ReasonCode);
    }

    [Fact]
    public async Task RelatedReplacement_marks_previous_effective_superseded()
    {
        var f = Fixture();
        var previous = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");
        var next = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000002", code: "GMG-QMS-SOP-0002");

        var r = await f.Service.TransitionAsync(next.Id, To("Effective", replacement: previous.Id), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        var prevAfter = f.Register.Items.Single(x => x.Id == previous.Id);
        Assert.Equal(ControlledDocumentLifecycleStatus.Superseded, prevAfter.LifecycleStatus);
        Assert.Equal(next.Id, prevAfter.SupersededByRegisterEntryId);
        Assert.Equal(previous.Id, f.Register.Items.Single(x => x.Id == next.Id).SupersedesRegisterEntryId);
    }

    [Fact]
    public async Task Duplicate_effective_for_same_uid_is_blocked()
    {
        var f = Fixture();
        SeedEntry(f, ControlledDocumentLifecycleStatus.Effective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");
        var next = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(next.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.DuplicateEffective, r.ReasonCode);
    }

    [Fact]
    public async Task Transition_record_created_for_each_transition()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft);
        await f.Service.TransitionAsync(e.Id, To("InReview"), Corr, CancellationToken.None);
        await f.Service.TransitionAsync(e.Id, To("ApprovedPendingEffective"), Corr, CancellationToken.None);

        var history = await f.Service.GetTransitionsAsync(e.Id, Corr, CancellationToken.None);

        Assert.True(history.IsSuccessful);
        Assert.Equal(2, history.Data!.Count);
    }

    [Fact]
    public async Task Transition_records_are_tenant_scoped()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft);
        await f.Service.TransitionAsync(e.Id, To("InReview"), Corr, CancellationToken.None);
        f.Transitions.Items.Add(new DocumentLifecycleTransitionRecord { Id = Guid.NewGuid(), TenantId = OtherTenantId, RegisterEntryId = e.Id });

        var history = await f.Service.GetTransitionsAsync(e.Id, Corr, CancellationToken.None);

        Assert.Single(history.Data!);
    }

    [Fact]
    public async Task Cross_tenant_transition_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft, tenantId: OtherTenantId);

        var r = await f.Service.TransitionAsync(foreign.Id, To("InReview"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Protected_uid_and_code_unchanged_by_lifecycle_transitions()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");
        await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);
        await f.Service.TransitionAsync(e.Id, To("UnderRevision"), Corr, CancellationToken.None);

        var after = f.Register.Items.Single(x => x.Id == e.Id);
        Assert.Equal("UID-0000001", after.PermanentUid);
        Assert.Equal("GMG-QMS-SOP-0001", after.DocumentCode);
    }

    [Fact]
    public async Task Stale_expected_version_is_rejected()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft);

        var r = await f.Service.TransitionAsync(e.Id, To("InReview", expectedVersion: 999), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.StaleVersion, r.ReasonCode);
    }

    [Fact]
    public async Task MarkEffective_without_evidence_or_gate_succeeds_with_warnings()
    {
        var f = Fixture();
        var e = SeedEntry(f, ControlledDocumentLifecycleStatus.ApprovedPendingEffective, uid: "UID-0000001", code: "GMG-QMS-SOP-0001");

        var r = await f.Service.TransitionAsync(e.Id, To("Effective"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.NotEmpty(r.Data!.Warnings); // release-gate + evidence warnings (FU10 pending), non-blocking by default
    }

    // ── fixtures ──────────────────────────────────────────────────────────────

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var transitions = new FakeTransitionRepo(tenant);
        var options = Options.Create(new DocumentLifecycleOptions());
        var service = new DocumentLifecycleService(register, transitions, tenant, new FakeUser(), options);
        return new Harness(service, register, transitions);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, ControlledDocumentLifecycleStatus status, string? uid = null, string? code = null, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop,
            DocumentType = DocumentType.Sop,
            Criticality = DocumentCriticality.Critical,
            IsControlledDocument = true,
            LifecycleStatus = status,
            PermanentUid = uid,
            DocumentCode = code,
            RegisterStatus = DocumentRegisterStatus.Active
        };
        f.Register.Items.Add(e);
        return e;
    }

    private sealed record Harness(DocumentLifecycleService Service, FakeRegisterRepo Register, FakeTransitionRepo Transitions);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444444");
        public string? Email => "fu08@example.test";
        public string? DisplayName => "FU08 Tester";
        public string ActorName => "fu08@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { Items.Add(entry); return Task.FromResult(entry); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == permanentUid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == documentCode));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == controlledDocumentId));

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default)
        {
            var q = Scoped;
            if (filter.RegisterStatus is { } rs) q = q.Where(x => x.RegisterStatus == rs);
            if (filter.LifecycleStatus is { } ls) q = q.Where(x => x.LifecycleStatus == ls);
            if (filter.Criticality is { } c) q = q.Where(x => x.Criticality == c);
            if (filter.DocumentClass is { } dc) q = q.Where(x => x.DocumentClass == dc);
            if (filter.OwnerCompanyId is { } oc) q = q.Where(x => x.OwnerCompanyId == oc);
            return Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(q.ToList());
        }

        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == entry.Id); if (i >= 0) Items[i] = entry; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeTransitionRepo(ITenantContext tenant) : IDocumentLifecycleTransitionRecordRepository
    {
        public List<DocumentLifecycleTransitionRecord> Items { get; } = [];
        private IEnumerable<DocumentLifecycleTransitionRecord> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord record, CancellationToken ct = default) { Items.Add(record); return Task.FromResult(record); }
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Scoped.Where(x => x.RegisterEntryId == registerEntryId).ToList());
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Scoped.ToList());
    }
}
