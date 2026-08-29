using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementApproval;
using Diten.Platform.Application.Features.DocumentManagementApproval.Services;
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
/// MOD-0029-FU09 — approval route matrix + segregation + evidence tests. Tenant-aware in-memory fakes exercise route
/// resolution, overlay merge, idempotency, role match, segregation rules, readiness and the FU08 approval-gate seam.
/// </summary>
public sealed class DocumentApprovalRouteTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Author = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid UserA = Guid.Parse("a0000000-0000-0000-0000-0000000000aa");
    private static readonly Guid UserB = Guid.Parse("a0000000-0000-0000-0000-0000000000bb");
    private const string Corr = "fu09-corr-1";

    private static ResolveApprovalRouteInput NoOverride => new();

    [Fact]
    public async Task Resolve_route_for_critical_sop_requires_GQD_and_QA()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("GQD:Approval", keys);
        Assert.Contains("QADocumentation:Review", keys);
        Assert.Equal("Group Quality Director", RequirementByKey(f, e.Id, "GQD:Approval").RequiredRoleDisplayName);
        Assert.NotEqual(Guid.Empty, RequirementByKey(f, e.Id, "GQD:Approval").RequiredRoleId);
        Assert.Equal("QA Documentation", RequirementByKey(f, e.Id, "QADocumentation:Review").RequiredRoleDisplayName);
        Assert.NotEqual(Guid.Empty, RequirementByKey(f, e.Id, "QADocumentation:Review").RequiredRoleId);
    }

    [Fact]
    public async Task Resolve_route_fails_closed_when_required_auth_role_is_missing()
    {
        var f = Fixture(new MissingApprovalRoleDirectory());
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);

        var result = await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.RoleConfigurationMissing, result.ReasonCode);
        Assert.Empty(f.Requirements.Items);
    }

    [Fact]
    public async Task Document_owner_requirement_is_bound_to_assigned_owner_user()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.WorkInstruction);
        e.ProcessOwnerUserId = UserA;

        var result = await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var owner = RequirementByKey(f, e.Id, "DocumentOwner:Approval");
        Assert.Equal(UserA, owner.RequiredUserId);
        Assert.Null(owner.RequiredRoleId);
    }

    [Fact]
    public async Task Document_owner_requirement_is_created_unassigned_when_owner_is_not_yet_selected()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.WorkInstruction);
        e.ProcessOwnerUserId = null;

        var result = await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var owner = RequirementByKey(f, e.Id, "DocumentOwner:Approval");
        Assert.Null(owner.RequiredUserId);
        Assert.Null(owner.RequiredRoleId);
    }

    [Fact]
    public async Task Resolve_route_for_RA_controlled_requires_GRA_plus_GQD()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride with { HasRaImpact = true }, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("GRA:Approval", keys);
        Assert.Contains("GQD:QualityConcurrence", keys);
    }

    [Fact]
    public async Task Resolve_route_for_PV_requires_QPPV_plus_GQD()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride with { HasPvImpact = true }, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("QPPV:Approval", keys);
        Assert.Contains("GQD:QualityConcurrence", keys);
    }

    [Fact]
    public async Task Resolve_route_for_batch_release_requires_QP()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride with { HasBatchReleaseImpact = true }, Corr, CancellationToken.None);

        Assert.Contains("QP:Approval", KeysOf(f, e.Id));
    }

    [Fact]
    public async Task Resolve_route_for_quality_agreement_requires_GQD_and_Legal()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.QualityTechnicalAgreementSdea);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("GQD:Approval", keys);
        Assert.Contains("Legal:LegalReview", keys);
    }

    [Fact]
    public async Task Resolve_route_for_DMS_CSV_requires_GQD_and_ITCSV()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride with { HasDmsCsvImpact = true }, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("GQD:Approval", keys);
        Assert.Contains("ITCSVOwner:TechnicalReview", keys);
    }

    [Fact]
    public async Task Resolve_route_for_group_policy_requires_CEO_and_GQD()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.PolicyGovernance);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var keys = KeysOf(f, e.Id);

        Assert.Contains("CEO:Endorsement", keys);
        Assert.Contains("GQD:Approval", keys);
    }

    [Fact]
    public async Task Duplicate_overlay_requirements_are_merged()
    {
        var f = Fixture();
        // SOP class adds GQD:Approval; DMS/CSV overlay also adds GQD:Approval — must merge to one.
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride with { HasDmsCsvImpact = true }, Corr, CancellationToken.None);

        Assert.Equal(1, f.Requirements.Items.Count(x => x.RegisterEntryId == e.Id && x.RequirementKey == "GQD:Approval"));
    }

    [Fact]
    public async Task Requirements_are_idempotent_on_second_resolve()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var first = f.Requirements.Items.Count(x => x.RegisterEntryId == e.Id);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var second = f.Requirements.Items.Count(x => x.RegisterEntryId == e.Id);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Stale_pending_requirement_is_retired_when_route_shrinks()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        Assert.Contains("GQD:Approval", KeysOf(f, e.Id));

        // Lower the route: a Major work instruction no longer needs a GQD approval.
        e.Criticality = DocumentCriticality.Major;
        e.DocumentClass = ControlledDocumentClass.WorkInstruction;
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        // The now-irrelevant PENDING requirement is soft-deleted (retired), so reads no longer surface it.
        var gqd = f.Requirements.Items.Single(x => x.RegisterEntryId == e.Id && x.RequirementKey == "GQD:Approval");
        Assert.True(gqd.IsDeleted);
        var reqs = await f.Service.GetRequirementsAsync(e.Id, Corr, CancellationToken.None);
        Assert.DoesNotContain(reqs.Data!, r => r.RequirementKey == "GQD:Approval");
    }

    [Fact]
    public async Task Completed_requirement_is_preserved_when_route_shrinks()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        await Complete(f, e.Id, "GQD:Approval", UserA, "GQD");

        // Even if the route later no longer requires it, a decision already recorded is immutable evidence.
        e.Criticality = DocumentCriticality.Major;
        e.DocumentClass = ControlledDocumentClass.WorkInstruction;
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        var gqd = f.Requirements.Items.Single(x => x.RegisterEntryId == e.Id && x.RequirementKey == "GQD:Approval");
        Assert.False(gqd.IsDeleted);
        Assert.Equal(ApprovalRequirementStatus.Completed, gqd.Status);
    }

    [Fact]
    public async Task Record_evidence_completes_matching_requirement()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var gqd = RequirementByKey(f, e.Id, "GQD:Approval");

        var r = await f.Service.RecordEvidenceAsync(e.Id, new RecordApprovalEvidenceInput(gqd.Id, "Approved", UserA, "GQD", "SIGN-1", null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(ApprovalRequirementStatus.Completed, f.Requirements.Items.Single(x => x.Id == gqd.Id).Status);
    }

    [Fact]
    public async Task Record_evidence_rejects_wrong_role()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var gqd = RequirementByKey(f, e.Id, "GQD:Approval");

        var r = await f.Service.RecordEvidenceAsync(e.Id, new RecordApprovalEvidenceInput(gqd.Id, "Approved", UserA, "QADocumentation", null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.WrongRole, r.ReasonCode);
    }

    [Fact]
    public async Task Record_evidence_rejects_client_side_user_impersonation()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var gqd = RequirementByKey(f, e.Id, "GQD:Approval");

        // The authenticated fake is UserA, while the request claims UserB.
        var r = await f.Service.RecordEvidenceAsync(e.Id,
            new RecordApprovalEvidenceInput(gqd.Id, "Approved", UserB, "GQD", null, null),
            Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.ApproverIdentityMismatch, r.ReasonCode);
    }

    [Fact]
    public async Task Record_evidence_rejects_user_without_auth_role_assignment()
    {
        var f = Fixture(new FakeApprovalRoleDirectory(authorizeAssignments: false));
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var gqd = RequirementByKey(f, e.Id, "GQD:Approval");

        var r = await f.Service.RecordEvidenceAsync(e.Id,
            new RecordApprovalEvidenceInput(gqd.Id, "Approved", UserA, "GQD", null, null),
            Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.ApproverNotAssigned, r.ReasonCode);
    }

    [Fact]
    public async Task Document_owner_evidence_rejects_non_owner()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.WorkInstruction);
        e.ProcessOwnerUserId = UserB;
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var owner = RequirementByKey(f, e.Id, "DocumentOwner:Approval");

        var r = await f.Service.RecordEvidenceAsync(e.Id,
            new RecordApprovalEvidenceInput(owner.Id, "Approved", UserA, "DocumentOwner", null, null),
            Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.ApproverNotAssigned, r.ReasonCode);
    }

    [Fact]
    public async Task Author_cannot_be_sole_approver()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        // The author performs the only approval; a different user does the review.
        await Complete(f, e.Id, "GQD:Approval", Author, "GQD");
        var r = await Complete(f, e.Id, "QADocumentation:Review", UserB, "QADocumentation");

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.SegregationFailed, r.ReasonCode);
        Assert.Equal(ApprovalRequirementStatus.Pending,
            RequirementByKey(f, e.Id, "QADocumentation:Review").Status);
    }

    [Fact]
    public async Task Same_user_cannot_complete_all_mandatory_requirements_alone()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        await Complete(f, e.Id, "GQD:Approval", UserA, "GQD");
        var r = await Complete(f, e.Id, "QADocumentation:Review", UserA, "QADocumentation");

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.SegregationFailed, r.ReasonCode);
    }

    [Fact]
    public async Task Critical_process_owner_author_adds_independent_review_requirement()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        e.ProcessOwnerUserId = Author;

        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        Assert.Contains("IndependentQASenior:TechnicalReview", KeysOf(f, e.Id));
    }

    [Fact]
    public async Task Missing_author_identity_produces_segregation_failure()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: null);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        await Complete(f, e.Id, "GQD:Approval", UserA, "GQD");
        var r = await Complete(f, e.Id, "QADocumentation:Review", UserB, "QADocumentation");

        Assert.False(r.IsSuccessful);
        Assert.Equal(ApprovalReasonCodes.SegregationFailed, r.ReasonCode);
    }

    [Fact]
    public async Task Approval_readiness_pending_when_requirements_incomplete()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        var readiness = await f.Service.GetReadinessAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(ApprovalEvidenceState.Pending.ToString(), readiness.Data!.ApprovalEvidenceStatus);
        Assert.False(readiness.Data.Ready);
        Assert.NotEmpty(readiness.Data.MissingMandatoryRoles);
    }

    [Fact]
    public async Task Approval_readiness_does_not_report_author_as_sole_approver_while_other_mandatory_approvals_are_pending()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Major, ControlledDocumentClass.Sop, author: Author);
        e.ProcessOwnerUserId = Author;
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);

        await Complete(f, e.Id, "DocumentOwner:Approval", Author, "DocumentOwner");
        var readiness = await f.Service.GetReadinessAsync(e.Id, Corr, CancellationToken.None);

        Assert.Equal(ApprovalEvidenceState.Pending.ToString(), readiness.Data!.ApprovalEvidenceStatus);
        Assert.Empty(readiness.Data.SegregationFailures);
        Assert.Contains(ApprovalRequiredRole.GQD.ToString(), readiness.Data.MissingMandatoryRoles);
        Assert.Contains(ApprovalRequiredRole.QADocumentation.ToString(), readiness.Data.MissingMandatoryRoles);
    }

    [Fact]
    public async Task Approval_readiness_complete_when_all_done_and_segregation_passed()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        await Complete(f, e.Id, "GQD:Approval", UserA, "GQD");
        var r = await Complete(f, e.Id, "QADocumentation:Review", UserB, "QADocumentation");

        Assert.Equal(ApprovalEvidenceState.Complete.ToString(), r.Data!.ApprovalEvidenceStatus);
        Assert.True(r.Data.Ready);
    }

    [Fact]
    public async Task Rejected_evidence_sets_status_rejected()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var gqd = RequirementByKey(f, e.Id, "GQD:Approval");

        var r = await f.Service.RejectAsync(e.Id, new RejectApprovalInput(gqd.Id, UserA, "GQD", "insufficient controls", null), Corr, CancellationToken.None);

        Assert.Equal(ApprovalEvidenceState.Rejected.ToString(), r.Data!.ApprovalEvidenceStatus);
    }

    [Fact]
    public async Task ApprovalEvidenceStatus_updates_on_register_entry()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);

        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        Assert.Equal(ApprovalEvidenceState.Pending.ToString(), f.Register.Items.Single(x => x.Id == e.Id).ApprovalEvidenceStatus);

        await Complete(f, e.Id, "GQD:Approval", UserA, "GQD");
        await Complete(f, e.Id, "QADocumentation:Review", UserB, "QADocumentation");
        Assert.Equal(ApprovalEvidenceState.Complete.ToString(), f.Register.Items.Single(x => x.Id == e.Id).ApprovalEvidenceStatus);
    }

    [Fact]
    public async Task Evidence_history_is_appended_and_never_hard_deleted()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, author: Author);
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        await Complete(f, e.Id, "GQD:Approval", UserA, "GQD");
        await Complete(f, e.Id, "GQD:Approval", UserB, "GQD"); // re-record

        Assert.Equal(2, f.Evidence.Items.Count(x => x.RegisterEntryId == e.Id));
        Assert.DoesNotContain(f.Evidence.Items, x => x.IsDeleted);
    }

    [Fact]
    public async Task Cross_tenant_resolve_is_blocked()
    {
        var f = Fixture();
        var foreign = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop, tenantId: OtherTenantId);

        var r = await f.Service.ResolveRouteAsync(foreign.Id, NoOverride, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_InReview_to_ApprovedPendingEffective_is_blocked_even_when_legacy_option_false()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.InReview;
        var lifecycle = LifecycleService(f, requireApproval: false);

        var r = await lifecycle.TransitionAsync(e.Id, new TransitionDocumentLifecycleInput("ApprovedPendingEffective", null, null, null, null, null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.ApprovalIncomplete, r.ReasonCode);
    }

    [Fact]
    public async Task Lifecycle_blocks_when_option_true_and_approval_incomplete()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.InReview;
        var lifecycle = LifecycleService(f, requireApproval: true);

        var r = await lifecycle.TransitionAsync(e.Id, new TransitionDocumentLifecycleInput("ApprovedPendingEffective", null, null, null, null, null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.ApprovalIncomplete, r.ReasonCode);
    }

    [Fact]
    public async Task Lifecycle_state_disables_approval_transition_while_requirements_are_incomplete()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.InReview;
        await f.Service.ResolveRouteAsync(e.Id, NoOverride, Corr, CancellationToken.None);
        var lifecycle = LifecycleService(f, requireApproval: false);

        var state = await lifecycle.GetStateAsync(e.Id, Corr, CancellationToken.None);

        Assert.False(state.Data!.CanMarkApprovedPendingEffective);
        Assert.Contains(state.Data.Warnings, warning => warning.Contains("Mandatory approvals", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lifecycle_effective_is_blocked_unless_approval_status_is_complete()
    {
        var f = Fixture();
        var e = SeedEntry(f, DocumentCriticality.Critical, ControlledDocumentClass.Sop);
        e.LifecycleStatus = ControlledDocumentLifecycleStatus.ApprovedPendingEffective;
        e.PermanentUid = "UID-TEST";
        e.DocumentCode = "DOC-TEST";
        e.ApprovalEvidenceStatus = ApprovalEvidenceState.Pending.ToString();
        var lifecycle = LifecycleService(f, requireApproval: false);

        var state = await lifecycle.GetStateAsync(e.Id, Corr, CancellationToken.None);
        var transition = await lifecycle.TransitionAsync(e.Id,
            new TransitionDocumentLifecycleInput("Effective", null, null, null, null, null, null),
            Corr, CancellationToken.None);

        Assert.False(state.Data!.CanMarkEffective);
        Assert.False(transition.IsSuccessful);
        Assert.Equal(LifecycleReasonCodes.ApprovalEvidenceMissing, transition.ReasonCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> KeysOf(Harness f, Guid entryId) =>
        f.Requirements.Items.Where(x => x.RegisterEntryId == entryId).Select(x => x.RequirementKey).ToList();

    private static DocumentApprovalRequirement RequirementByKey(Harness f, Guid entryId, string key) =>
        f.Requirements.Items.Single(x => x.RegisterEntryId == entryId && x.RequirementKey == key);

    private static Task<Diten.Platform.Application.Common.Response<ApprovalReadinessModel>> Complete(Harness f, Guid entryId, string key, Guid userId, string role)
    {
        var req = RequirementByKey(f, entryId, key);
        f.User.UserIdValue = userId;
        return f.Service.RecordEvidenceAsync(entryId, new RecordApprovalEvidenceInput(req.Id, "Approved", userId, role, null, null), Corr, CancellationToken.None);
    }

    private static Harness Fixture(IApprovalRoleDirectory? roleDirectory = null)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var register = new FakeRegisterRepo(tenant);
        var requirements = new FakeRequirementRepo(tenant);
        var evidence = new FakeEvidenceRepo(tenant);
        var user = new FakeUser();
        var service = new DocumentApprovalService(register, requirements, evidence,
            new DocumentApprovalRouteResolver(), new DocumentSegregationRuleEvaluator(),
            roleDirectory ?? new FakeApprovalRoleDirectory(), tenant, user);
        return new Harness(service, register, requirements, evidence, tenant, user);
    }

    private static DocumentLifecycleService LifecycleService(Harness f, bool requireApproval)
    {
        var gate = new ApprovedPendingEffectiveGate(f.Requirements, new DocumentSegregationRuleEvaluator(),
            Options.Create(new DocumentApprovalOptions { RequireApprovalForApprovedPendingEffective = requireApproval }));
        return new DocumentLifecycleService(f.Register, new FakeTransitionRepo(f.Tenant), f.Tenant, new FakeUser(),
            Options.Create(new DocumentLifecycleOptions()), gate);
    }

    private static DocumentMasterRegisterEntry SeedEntry(
        Harness f, DocumentCriticality criticality, ControlledDocumentClass documentClass, Guid? author = null, Guid? tenantId = null)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = documentClass,
            DocumentType = DocumentType.Sop,
            Criticality = criticality,
            IsControlledDocument = true,
            ControlledDocumentId = Guid.NewGuid(),
            LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Compatible,
            AuthorUserId = author,
            ProcessOwnerUserId = UserB,
            RegisterStatus = DocumentRegisterStatus.Active,
            LifecycleStatus = ControlledDocumentLifecycleStatus.Draft
        };
        f.Register.Items.Add(e);
        return e;
    }

    private sealed record Harness(
        DocumentApprovalService Service, FakeRegisterRepo Register, FakeRequirementRepo Requirements,
        FakeEvidenceRepo Evidence, ITenantContext Tenant, FakeUser User);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserIdValue { get; set; } = UserA;
        public Guid UserId => UserIdValue;
        public string? Email => "fu09@example.test";
        public string? DisplayName => "FU09 Tester";
        public string ActorName => "fu09@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeApprovalRoleDirectory(bool authorizeAssignments = true) : IApprovalRoleDirectory
    {
        public Task<IReadOnlyDictionary<string, ApprovalDirectoryRole>> ResolveAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken ct = default)
        {
            IReadOnlyDictionary<string, ApprovalDirectoryRole> roles = roleNames.ToDictionary(
                name => name,
                name => new ApprovalDirectoryRole(
                    StableRoleId(name),
                    name,
                    name switch
                    {
                        "GQD" => "Group Quality Director",
                        "QADocumentation" => "QA Documentation",
                        _ => name
                    }),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(roles);
        }

        public Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
            Task.FromResult(authorizeAssignments && userId != Guid.Empty && roleId != Guid.Empty);

        private static Guid StableRoleId(string name)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name));
            return new Guid(bytes[..16]);
        }
    }

    private sealed class MissingApprovalRoleDirectory : IApprovalRoleDirectory
    {
        public Task<IReadOnlyDictionary<string, ApprovalDirectoryRole>> ResolveAsync(
            IReadOnlyCollection<string> roleNames,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, ApprovalDirectoryRole>>(
                new Dictionary<string, ApprovalDirectoryRole>(StringComparer.OrdinalIgnoreCase));

        public Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken ct = default) =>
            Task.FromResult(false);
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
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == entry.Id); if (i >= 0) Items[i] = entry; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeRequirementRepo(ITenantContext tenant) : IDocumentApprovalRequirementRepository
    {
        public List<DocumentApprovalRequirement> Items { get; } = [];
        private IEnumerable<DocumentApprovalRequirement> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentApprovalRequirement> CreateAsync(DocumentApprovalRequirement requirement, CancellationToken ct = default)
        {
            var stored = Snapshot(requirement);
            Items.Add(stored);
            return Task.FromResult(Snapshot(stored));
        }
        public Task<DocumentApprovalRequirement?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id) is { } item ? Snapshot(item) : null);
        public Task<IReadOnlyList<DocumentApprovalRequirement>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentApprovalRequirement>>(
                Scoped.Where(x => x.RegisterEntryId == registerEntryId).Select(Snapshot).ToList());
        public Task<bool> UpdateAsync(DocumentApprovalRequirement requirement, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == requirement.Id);
            if (i >= 0) Items[i] = Snapshot(requirement);
            return Task.FromResult(i >= 0);
        }

        private static DocumentApprovalRequirement Snapshot(DocumentApprovalRequirement source) => new()
        {
            Id = source.Id,
            TenantId = source.TenantId,
            CreatedAt = source.CreatedAt,
            CreatedBy = source.CreatedBy,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy,
            IsDeleted = source.IsDeleted,
            Version = source.Version,
            RegisterEntryId = source.RegisterEntryId,
            RequirementKey = source.RequirementKey,
            RequirementType = source.RequirementType,
            RequiredRole = source.RequiredRole,
            RequiredRoleId = source.RequiredRoleId,
            RequiredRoleName = source.RequiredRoleName,
            RequiredRoleDisplayName = source.RequiredRoleDisplayName,
            RequiredUserId = source.RequiredUserId,
            RequiredFunction = source.RequiredFunction,
            IsMandatory = source.IsMandatory,
            IsNonDelegable = source.IsNonDelegable,
            SourceRule = source.SourceRule,
            Status = source.Status,
            CompletedByUserId = source.CompletedByUserId,
            CompletedByRole = source.CompletedByRole,
            CompletedAt = source.CompletedAt,
            EvidenceReference = source.EvidenceReference,
            Comment = source.Comment,
            DeletedAt = source.DeletedAt
        };
    }

    private sealed class FakeEvidenceRepo(ITenantContext tenant) : IDocumentApprovalEvidenceRepository
    {
        public List<DocumentApprovalEvidence> Items { get; } = [];
        private IEnumerable<DocumentApprovalEvidence> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentApprovalEvidence> CreateAsync(DocumentApprovalEvidence evidence, CancellationToken ct = default) { Items.Add(evidence); return Task.FromResult(evidence); }
        public Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentApprovalEvidence>>(Scoped.Where(x => x.RegisterEntryId == registerEntryId).ToList());
        public Task<IReadOnlyList<DocumentApprovalEvidence>> GetByRequirementAsync(Guid requirementId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentApprovalEvidence>>(Scoped.Where(x => x.RequirementId == requirementId).ToList());
    }

    private sealed class FakeTransitionRepo(ITenantContext tenant) : IDocumentLifecycleTransitionRecordRepository
    {
        public List<DocumentLifecycleTransitionRecord> Items { get; } = [];
        public Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord record, CancellationToken ct = default) { Items.Add(record); return Task.FromResult(record); }
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == registerEntryId).ToList());
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(Items.Where(x => x.TenantId == tenant.TenantId).ToList());
    }
}
