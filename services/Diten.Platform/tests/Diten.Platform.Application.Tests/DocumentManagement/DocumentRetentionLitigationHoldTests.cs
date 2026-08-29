using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using Diten.Platform.Application.Features.DocumentManagementRetention.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU15 — Retention Schedule &amp; Litigation Hold tests (GMG-QMS-SOP-0001 §22). Tenant-aware in-memory
/// fakes exercise policy authoring, the longest-applicable retention rule, the fail-closed evaluator, litigation
/// hold blocking, the dual Legal + GQD release control, and the disposition request flow.
///
/// The most important assertions in this file are the NEGATIVE ones: that nothing is ever deleted, and that a
/// disposition "execution" leaves the subject record fully intact.
/// </summary>
public sealed class DocumentRetentionLitigationHoldTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu15-corr-1";

    // ── retention policy ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_retention_policy()
    {
        var f = Fixture();

        var r = await f.Policies.CreateAsync(TenYearPolicy(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Draft", r.Data!.PolicyStatus);
        Assert.Equal("CONTROLLED-DOC-10Y", r.Data.PolicyKey);
    }

    [Fact]
    public async Task Create_policy_validates_key_name_and_years()
    {
        var f = Fixture();

        var noKey = await f.Policies.CreateAsync(TenYearPolicy() with { PolicyKey = " " }, Corr, CancellationToken.None);
        var noName = await f.Policies.CreateAsync(TenYearPolicy() with { PolicyName = "" }, Corr, CancellationToken.None);
        var negative = await f.Policies.CreateAsync(TenYearPolicy() with { MinimumRetentionYears = -1 }, Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.PolicyKeyRequired, noKey.ReasonCode);
        Assert.Equal(RetentionReasonCodes.PolicyNameRequired, noName.ReasonCode);
        Assert.Equal(RetentionReasonCodes.RetentionYearsInvalid, negative.ReasonCode);
    }

    [Fact]
    public async Task Activate_retention_policy()
    {
        var f = Fixture();
        var created = await f.Policies.CreateAsync(TenYearPolicy(), Corr, CancellationToken.None);

        var r = await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.Equal("Active", r.Data!.PolicyStatus);
    }

    [Fact]
    public async Task Retiring_a_policy_is_a_status_change_not_a_delete()
    {
        var f = Fixture();
        var created = await f.Policies.CreateAsync(TenYearPolicy(), Corr, CancellationToken.None);

        var r = await f.Policies.RetireAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.Equal("Retired", r.Data!.PolicyStatus);
        Assert.Single(f.PolicyRepo.Items);
        Assert.DoesNotContain(f.PolicyRepo.Items, x => x.IsDeleted);
    }

    [Fact]
    public async Task Controlled_document_policy_retains_while_effective_plus_10_years()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, TenYearPolicy());
        // A document retired 11 years ago: past the 10-year post-retirement period.
        var entry = SeedRegisterEntry(f, ControlledDocumentLifecycleStatus.Retired, DateTimeOffset.UtcNow.AddYears(-11));

        var r = await f.Evaluator.EvaluateAsync(RegisterSubject(entry.Id), Corr, CancellationToken.None);

        Assert.Equal("Eligible", r.Data!.EvaluationStatus);
        Assert.True(r.Data.IsDispositionEligible);
        Assert.Equal(10, (r.Data.RetentionDueDate!.Value - r.Data.RetentionTriggerDate!.Value).Days / 365);
    }

    [Fact]
    public async Task Effective_document_is_retained_regardless_of_elapsed_period()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, TenYearPolicy());
        // Effective for 30 years — RetainWhileEffective must still block disposition.
        var entry = SeedRegisterEntry(f, ControlledDocumentLifecycleStatus.Effective, DateTimeOffset.UtcNow.AddYears(-30));

        var r = await f.Evaluator.EvaluateAsync(RegisterSubject(entry.Id), Corr, CancellationToken.None);

        Assert.Equal("Current", r.Data!.EvaluationStatus);
        Assert.False(r.Data.IsDispositionEligible);
        Assert.Contains("still Effective", r.Data.EvaluationNote);
    }

    [Fact]
    public async Task Approval_evidence_policy_retains_at_least_10_years()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var subjectId = Guid.NewGuid();

        var tooEarly = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-9)), Corr, CancellationToken.None);
        var elapsed = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-11)), Corr, CancellationToken.None);

        Assert.False(tooEarly.Data!.IsDispositionEligible);
        Assert.Equal("Current", tooEarly.Data.EvaluationStatus);
        Assert.True(elapsed.Data!.IsDispositionEligible);
    }

    [Fact]
    public async Task Identifier_allocation_ledger_is_never_disposition_eligible()
    {
        var f = Fixture();
        // Even with a short, permissive policy and a very old trigger date, the ledger must never be eligible.
        await ActivePolicyAsync(f, TenYearPolicy() with
        {
            PolicyKey = "UID-LEDGER",
            SubjectType = nameof(RetentionSubjectType.IdentifierAllocationLedger),
            MinimumRetentionYears = 1,
            RetainWhileEffective = false,
            RetainAfterRetirementYears = null,
            RetainAfterSupersessionYears = null
        });

        var r = await f.Evaluator.EvaluateAsync(new EvaluateRetentionInput(
            nameof(RetentionSubjectType.IdentifierAllocationLedger), Guid.NewGuid(), null, null,
            DateTimeOffset.UtcNow.AddYears(-50), null), Corr, CancellationToken.None);

        Assert.False(r.Data!.IsDispositionEligible);
        Assert.True(r.Data.IsPermanentRetention);
        Assert.Contains("permanent record", r.Data.EvaluationNote);
    }

    [Fact]
    public async Task Permanent_retention_policy_is_never_eligible()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy() with { PolicyKey = "PERMANENT", IsPermanentRetention = true });

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-99)), Corr, CancellationToken.None);

        Assert.False(r.Data!.IsDispositionEligible);
        Assert.True(r.Data.IsPermanentRetention);
    }

    [Fact]
    public async Task Longest_applicable_policy_wins()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy() with { PolicyKey = "SHORT-5Y", MinimumRetentionYears = 5 });
        await ActivePolicyAsync(f, EvidencePolicy() with { PolicyKey = "LONG-25Y", MinimumRetentionYears = 25 });

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-10)), Corr, CancellationToken.None);

        // 10 years elapsed clears the 5-year policy but not the 25-year one — the longest requirement governs.
        Assert.Equal("LONG-25Y", r.Data!.PolicyKey);
        Assert.False(r.Data.IsDispositionEligible);
    }

    [Fact]
    public async Task Longest_applicable_uses_the_longest_period_within_a_policy()
    {
        var f = Fixture();
        // Minimum is 2 years but post-supersession is 15 — the policy's own longest period must win.
        await ActivePolicyAsync(f, EvidencePolicy() with
        {
            PolicyKey = "MIXED", MinimumRetentionYears = 2, RetainAfterSupersessionYears = 15
        });

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-10)), Corr, CancellationToken.None);

        Assert.False(r.Data!.IsDispositionEligible);
        Assert.Equal(15, f.PolicyRepo.Items.Single(p => p.PolicyKey == "MIXED").EffectiveRetentionYears());
    }

    [Fact]
    public async Task Evaluate_subject_calculates_retention_due_date()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var trigger = new DateTimeOffset(2020, 3, 15, 0, 0, 0, TimeSpan.Zero);

        var r = await f.Evaluator.EvaluateAsync(EvidenceSubject(Guid.NewGuid(), trigger), Corr, CancellationToken.None);

        Assert.Equal(trigger, r.Data!.RetentionTriggerDate);
        Assert.Equal(trigger.AddYears(10), r.Data.RetentionDueDate);
    }

    [Fact]
    public async Task Missing_policy_results_in_missing_policy_and_not_eligible()
    {
        var f = Fixture(); // no policies at all

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-50)), Corr, CancellationToken.None);

        Assert.Equal("MissingPolicy", r.Data!.EvaluationStatus);
        Assert.False(r.Data.IsDispositionEligible);
    }

    [Fact]
    public async Task Draft_policy_does_not_apply()
    {
        var f = Fixture();
        await f.Policies.CreateAsync(EvidencePolicy(), Corr, CancellationToken.None); // created but NOT activated

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-50)), Corr, CancellationToken.None);

        Assert.Equal("MissingPolicy", r.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Missing_trigger_date_results_in_missing_trigger_and_not_eligible()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), triggerDate: null), Corr, CancellationToken.None);

        Assert.Equal("MissingTriggerDate", r.Data!.EvaluationStatus);
        Assert.False(r.Data.IsDispositionEligible);
    }

    [Fact]
    public async Task Subject_is_not_eligible_before_the_due_date()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-3)), Corr, CancellationToken.None);

        Assert.Equal("Current", r.Data!.EvaluationStatus);
        Assert.False(r.Data.IsDispositionEligible);
    }

    [Fact]
    public async Task Subject_is_eligible_after_the_due_date_when_no_hold_exists()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-12)), Corr, CancellationToken.None);

        Assert.Equal("Eligible", r.Data!.EvaluationStatus);
        Assert.True(r.Data.IsDispositionEligible);
        Assert.False(r.Data.IsBlockedByLegalHold);
    }

    [Fact]
    public async Task Re_evaluating_updates_the_snapshot_in_place()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var subjectId = Guid.NewGuid();

        await f.Evaluator.EvaluateAsync(EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-3)), Corr, CancellationToken.None);
        await f.Evaluator.EvaluateAsync(EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-12)), Corr, CancellationToken.None);

        var snapshot = Assert.Single(f.SubjectRepo.Items);
        Assert.True(snapshot.IsDispositionEligible);
    }

    // ── legal hold ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_legal_hold()
    {
        var f = Fixture();

        var r = await f.Holds.CreateAsync(GlobalHold(), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Draft", r.Data!.HoldStatus);
        Assert.Equal("GlobalDocumentControl", r.Data.ScopeType);
    }

    [Fact]
    public async Task Create_legal_hold_requires_title_and_usable_scope()
    {
        var f = Fixture();

        var noTitle = await f.Holds.CreateAsync(GlobalHold() with { HoldTitle = " " }, Corr, CancellationToken.None);
        var emptyScope = await f.Holds.CreateAsync(GlobalHold() with
        {
            ScopeType = nameof(LegalHoldScopeType.RegisterEntry), RegisterEntryIds = []
        }, Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.HoldTitleRequired, noTitle.ReasonCode);
        Assert.Equal(RetentionReasonCodes.HoldScopeRequired, emptyScope.ReasonCode);
    }

    [Fact]
    public async Task Activate_legal_hold_requires_legal_approval_evidence()
    {
        var f = Fixture();
        var created = await f.Holds.CreateAsync(GlobalHold(), Corr, CancellationToken.None);

        var blocked = await f.Holds.ActivateAsync(created.Data!.Id,
            new ActivateLegalHoldInput("  ", null, null), Corr, CancellationToken.None);
        Assert.Equal(RetentionReasonCodes.HoldLegalApprovalRequired, blocked.ReasonCode);
        Assert.Equal(LegalHoldStatus.Draft, f.HoldRepo.Items.Single().HoldStatus);

        var ok = await f.Holds.ActivateAsync(created.Data.Id,
            new ActivateLegalHoldInput("LEGAL-APPROVAL-1", null, null), Corr, CancellationToken.None);
        Assert.Equal("Active", ok.Data!.HoldStatus);
        Assert.NotNull(ok.Data.IssuedAt);
    }

    [Fact]
    public async Task Active_legal_hold_blocks_disposition_eligibility()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        await ActiveHoldAsync(f, GlobalHold());

        // Retention long elapsed, but the hold must win.
        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);

        Assert.Equal("BlockedByHold", r.Data!.EvaluationStatus);
        Assert.False(r.Data.IsDispositionEligible);
        Assert.True(r.Data.IsBlockedByLegalHold);
        Assert.Single(r.Data.ActiveLegalHoldIds);
    }

    [Fact]
    public async Task Release_legal_hold_requires_both_legal_approval_and_GQD_concurrence()
    {
        var f = Fixture();
        var hold = await ActiveHoldAsync(f, GlobalHold());

        var noLegal = await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("", "GQD-1"), Corr, CancellationToken.None);
        Assert.Equal(RetentionReasonCodes.HoldReleaseApprovalRequired, noLegal.ReasonCode);

        var noGqd = await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("LEGAL-REL-1", ""), Corr, CancellationToken.None);
        Assert.Equal(RetentionReasonCodes.HoldReleaseConcurrenceRequired, noGqd.ReasonCode);

        // Still active — neither partial attempt released it.
        Assert.Equal(LegalHoldStatus.Active, f.HoldRepo.Items.Single().HoldStatus);

        var ok = await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("LEGAL-REL-1", "GQD-CONCUR-1"), Corr, CancellationToken.None);
        Assert.Equal("Released", ok.Data!.HoldStatus);
        Assert.Equal("LEGAL-REL-1", ok.Data.ReleaseLegalApprovalReference);
        Assert.Equal("GQD-CONCUR-1", ok.Data.ReleaseGqdConcurrenceReference);
        Assert.NotNull(ok.Data.ReleasedAt);
    }

    [Fact]
    public async Task Released_hold_no_longer_blocks_disposition()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var hold = await ActiveHoldAsync(f, GlobalHold());
        var subjectId = Guid.NewGuid();

        var blocked = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);
        Assert.Equal("BlockedByHold", blocked.Data!.EvaluationStatus);

        await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("LEGAL-REL-1", "GQD-1"), Corr, CancellationToken.None);

        var after = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);
        Assert.Equal("Eligible", after.Data!.EvaluationStatus);
        Assert.True(after.Data.IsDispositionEligible);
    }

    [Fact]
    public async Task Releasing_a_hold_preserves_the_issuance_evidence_trail()
    {
        var f = Fixture();
        var hold = await ActiveHoldAsync(f, GlobalHold());

        await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("LEGAL-REL-1", "GQD-1"), Corr, CancellationToken.None);

        var stored = f.HoldRepo.Items.Single();
        Assert.Equal("LEGAL-APPROVAL-1", stored.LegalApprovalEvidenceReference); // issuance evidence intact
        Assert.NotNull(stored.IssuedAt);
        Assert.NotNull(stored.ReleasedAt);
        Assert.False(stored.IsDeleted);
    }

    [Fact]
    public async Task Global_document_control_hold_blocks_all_document_subjects()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        await ActivePolicyAsync(f, TenYearPolicy());
        await ActiveHoldAsync(f, GlobalHold());
        var entry = SeedRegisterEntry(f, ControlledDocumentLifecycleStatus.Retired, DateTimeOffset.UtcNow.AddYears(-30));

        var evidence = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);
        var register = await f.Evaluator.EvaluateAsync(RegisterSubject(entry.Id), Corr, CancellationToken.None);

        Assert.Equal("BlockedByHold", evidence.Data!.EvaluationStatus);
        Assert.Equal("BlockedByHold", register.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Register_entry_hold_blocks_linked_subjects()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var entry = SeedRegisterEntry(f, ControlledDocumentLifecycleStatus.Retired, DateTimeOffset.UtcNow.AddYears(-30));
        await ActiveHoldAsync(f, GlobalHold() with
        {
            ScopeType = nameof(LegalHoldScopeType.RegisterEntry),
            RegisterEntryIds = [entry.Id]
        });

        // Evidence linked to the held register entry is blocked...
        var linked = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)) with { RegisterEntryId = entry.Id },
            Corr, CancellationToken.None);
        Assert.Equal("BlockedByHold", linked.Data!.EvaluationStatus);

        // ...while unrelated evidence is unaffected.
        var unrelated = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);
        Assert.Equal("Eligible", unrelated.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task External_document_hold_blocks_external_records()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy() with
        {
            PolicyKey = "EXT-IMPACT-10Y",
            SubjectType = nameof(RetentionSubjectType.ExternalDocumentImpactAssessment)
        });
        var externalId = Guid.NewGuid();
        await ActiveHoldAsync(f, GlobalHold() with
        {
            ScopeType = nameof(LegalHoldScopeType.ExternalDocument),
            ExternalDocumentIds = [externalId]
        });

        var r = await f.Evaluator.EvaluateAsync(new EvaluateRetentionInput(
            nameof(RetentionSubjectType.ExternalDocumentImpactAssessment), Guid.NewGuid(), externalId, null,
            DateTimeOffset.UtcNow.AddYears(-30), null), Corr, CancellationToken.None);

        Assert.Equal("BlockedByHold", r.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Subject_type_scoped_hold_blocks_only_that_subject_type()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        await ActivePolicyAsync(f, EvidencePolicy() with
        {
            PolicyKey = "GATE-10Y", SubjectType = nameof(RetentionSubjectType.ReleaseGateEvidence)
        });
        await ActiveHoldAsync(f, GlobalHold() with
        {
            ScopeType = nameof(LegalHoldScopeType.SubjectType),
            SubjectTypes = [nameof(RetentionSubjectType.ApprovalEvidence)]
        });

        var approval = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);
        var gate = await f.Evaluator.EvaluateAsync(new EvaluateRetentionInput(
            nameof(RetentionSubjectType.ReleaseGateEvidence), Guid.NewGuid(), null, null,
            DateTimeOffset.UtcNow.AddYears(-30), null), Corr, CancellationToken.None);

        Assert.Equal("BlockedByHold", approval.Data!.EvaluationStatus);
        Assert.Equal("Eligible", gate.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Explicit_hold_membership_blocks_regardless_of_scope()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var subjectId = Guid.NewGuid();
        var hold = await ActiveHoldAsync(f, GlobalHold() with
        {
            ScopeType = nameof(LegalHoldScopeType.Repository),
            ScopeDescription = "Shared QA drive under investigation"
        });
        await f.Holds.AddSubjectAsync(hold.Id, nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, Corr, CancellationToken.None);

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);

        Assert.Equal("BlockedByHold", r.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Hold_membership_is_idempotent_and_released_as_history()
    {
        var f = Fixture();
        var subjectId = Guid.NewGuid();
        var hold = await ActiveHoldAsync(f, GlobalHold());

        var first = await f.Holds.AddSubjectAsync(hold.Id, nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, Corr, CancellationToken.None);
        var second = await f.Holds.AddSubjectAsync(hold.Id, nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, Corr, CancellationToken.None);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(f.HoldSubjectRepo.Items);

        await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("LEGAL-REL-1", "GQD-1"), Corr, CancellationToken.None);

        var membership = Assert.Single(f.HoldSubjectRepo.Items);
        Assert.Equal(LegalHoldSubjectStatus.Released, membership.Status);
        Assert.NotNull(membership.HoldReleasedAt);
        Assert.False(membership.IsDeleted); // history survives
    }

    [Fact]
    public async Task Custom_query_scope_never_blocks_implicitly()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        await ActiveHoldAsync(f, GlobalHold() with
        {
            ScopeType = nameof(LegalHoldScopeType.CustomQuery),
            ScopeDescription = "All documents mentioning project X"
        });

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);

        // FU15 does not execute custom scope queries, so it must not pretend to have blocked anything.
        Assert.Equal("Eligible", r.Data!.EvaluationStatus);
    }

    // ── disposition ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_disposition_request_for_an_eligible_subject()
    {
        var f = Fixture();
        var subjectId = await EligibleSubjectAsync(f);

        var r = await f.Dispositions.CreateAsync(
            new CreateDispositionRequestInput(nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, "Routine"),
            Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Draft", r.Data!.RequestStatus);
        Assert.Equal("Eligible", r.Data.EligibilityResult);
        Assert.StartsWith("DISP-", r.Data.RequestNumber);
    }

    [Fact]
    public async Task Disposition_submit_succeeds_for_an_eligible_subject()
    {
        var f = Fixture();
        var request = await DraftRequestAsync(f, await EligibleSubjectAsync(f));

        var r = await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Submitted", r.Data!.RequestStatus);
    }

    [Fact]
    public async Task Disposition_submit_is_blocked_by_an_active_hold()
    {
        var f = Fixture();
        var subjectId = await EligibleSubjectAsync(f);
        var request = await DraftRequestAsync(f, subjectId);
        await ActiveHoldAsync(f, GlobalHold()); // hold raised AFTER the subject was evaluated eligible

        var r = await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(RetentionReasonCodes.DispositionBlockedByHold, r.ReasonCode);
        Assert.Equal(DispositionRequestStatus.BlockedByHold, f.DispositionRepo.Items.Single().RequestStatus);
    }

    [Fact]
    public async Task Disposition_submit_is_blocked_when_the_subject_is_not_eligible()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        var subjectId = Guid.NewGuid();
        await f.Evaluator.EvaluateAsync(EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-2)), Corr, CancellationToken.None);
        var request = await DraftRequestAsync(f, subjectId);

        var r = await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.DispositionNotEligible, r.ReasonCode);
    }

    [Fact]
    public async Task Disposition_submit_is_blocked_when_the_subject_was_never_evaluated()
    {
        var f = Fixture();
        var request = await DraftRequestAsync(f, Guid.NewGuid()); // never evaluated

        var r = await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.DispositionNotEvaluated, r.ReasonCode);
    }

    [Fact]
    public async Task Disposition_approve_requires_approval_evidence()
    {
        var f = Fixture();
        var request = await SubmittedRequestAsync(f);

        var r = await f.Dispositions.ApproveAsync(request, new ApproveDispositionInput("  ", null), Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.DispositionApprovalEvidenceRequired, r.ReasonCode);
        Assert.Equal(DispositionRequestStatus.Submitted, f.DispositionRepo.Items.Single().RequestStatus);
    }

    [Fact]
    public async Task Disposition_execute_requires_an_approved_request()
    {
        var f = Fixture();
        var request = await SubmittedRequestAsync(f); // submitted but not approved

        var r = await f.Dispositions.ExecuteMarkerAsync(request, new ExecuteDispositionMarkerInput("EXEC-1"), Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.DispositionInvalidState, r.ReasonCode);
    }

    [Fact]
    public async Task Disposition_execute_writes_a_marker_but_does_not_delete_the_subject()
    {
        var f = Fixture();
        var subjectId = await EligibleSubjectAsync(f);
        var request = await DraftRequestAsync(f, subjectId);
        await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);
        await f.Dispositions.ApproveAsync(request, new ApproveDispositionInput("APPROVAL-1", null), Corr, CancellationToken.None);

        var r = await f.Dispositions.ExecuteMarkerAsync(request, new ExecuteDispositionMarkerInput("EXEC-EVIDENCE-1"), Corr, CancellationToken.None);

        Assert.Equal("ExecutedAsNoDeleteMarker", r.Data!.RequestStatus);
        Assert.Equal("EXEC-EVIDENCE-1", r.Data.ExecutionEvidenceReference);
        Assert.False(r.Data.SubjectWasDeleted);
        Assert.Contains("no deletion", r.Data.BoundaryStatement, StringComparison.OrdinalIgnoreCase);

        // THE POINT OF THIS FU: the retention subject snapshot is still there, untouched and undeleted.
        var snapshot = Assert.Single(f.SubjectRepo.Items);
        Assert.Equal(subjectId, snapshot.SubjectId);
        Assert.False(snapshot.IsDeleted);
    }

    [Fact]
    public async Task Disposition_execute_is_blocked_by_a_hold_raised_after_approval()
    {
        var f = Fixture();
        var request = await SubmittedRequestAsync(f);
        await f.Dispositions.ApproveAsync(request, new ApproveDispositionInput("APPROVAL-1", null), Corr, CancellationToken.None);
        await ActiveHoldAsync(f, GlobalHold()); // late hold must still stop execution

        var r = await f.Dispositions.ExecuteMarkerAsync(request, new ExecuteDispositionMarkerInput("EXEC-1"), Corr, CancellationToken.None);

        Assert.Equal(RetentionReasonCodes.DispositionBlockedByHold, r.ReasonCode);
        Assert.Equal(DispositionRequestStatus.BlockedByHold, f.DispositionRepo.Items.Single().RequestStatus);
    }

    [Fact]
    public async Task Disposition_reject_requires_a_reason()
    {
        var f = Fixture();
        var request = await SubmittedRequestAsync(f);

        var noReason = await f.Dispositions.RejectAsync(request, new RejectDispositionInput(" "), Corr, CancellationToken.None);
        Assert.Equal(RetentionReasonCodes.ValidationFailed, noReason.ReasonCode);

        var ok = await f.Dispositions.RejectAsync(request, new RejectDispositionInput("Still needed for audit"), Corr, CancellationToken.None);
        Assert.Equal("Rejected", ok.Data!.RequestStatus);
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_legal_hold_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentLegalHold
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, HoldKey = "FOREIGN", HoldTitle = "Foreign hold",
            HoldStatus = LegalHoldStatus.Active
        };
        f.HoldRepo.Items.Add(foreign);

        var read = await f.Holds.GetAsync(foreign.Id, Corr, CancellationToken.None);
        var release = await f.Holds.ReleaseAsync(foreign.Id, new ReleaseLegalHoldInput("L", "G"), Corr, CancellationToken.None);

        Assert.Equal(404, read.StatusCode);
        Assert.Equal(404, release.StatusCode);
        Assert.Equal(LegalHoldStatus.Active, f.HoldRepo.Items.Single(x => x.Id == foreign.Id).HoldStatus);
    }

    [Fact]
    public async Task Cross_tenant_hold_does_not_block_another_tenants_subject()
    {
        var f = Fixture();
        await ActivePolicyAsync(f, EvidencePolicy());
        f.HoldRepo.Items.Add(new DocumentLegalHold
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, HoldKey = "FOREIGN-GLOBAL", HoldTitle = "Foreign global hold",
            HoldStatus = LegalHoldStatus.Active, ScopeType = LegalHoldScopeType.GlobalDocumentControl
        });

        var r = await f.Evaluator.EvaluateAsync(
            EvidenceSubject(Guid.NewGuid(), DateTimeOffset.UtcNow.AddYears(-30)), Corr, CancellationToken.None);

        Assert.Equal("Eligible", r.Data!.EvaluationStatus);
    }

    [Fact]
    public async Task Cross_tenant_disposition_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentDispositionRequest
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, RequestNumber = "DISP-FOREIGN",
            SubjectType = RetentionSubjectType.ApprovalEvidence, SubjectId = Guid.NewGuid()
        };
        f.DispositionRepo.Items.Add(foreign);

        var read = await f.Dispositions.GetAsync(foreign.Id, Corr, CancellationToken.None);
        var submit = await f.Dispositions.SubmitAsync(foreign.Id, Corr, CancellationToken.None);

        Assert.Equal(404, read.StatusCode);
        Assert.Equal(404, submit.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_retention_policy_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentRetentionPolicy
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, PolicyKey = "FOREIGN", PolicyName = "Foreign policy"
        };
        f.PolicyRepo.Items.Add(foreign);

        var r = await f.Policies.GetAsync(foreign.Id, Corr, CancellationToken.None);

        Assert.Equal(404, r.StatusCode);
    }

    /// <summary>
    /// The defining guarantee of FU15: a complete policy → evaluate → hold → release → dispose → execute cycle
    /// removes NOTHING from any store.
    /// </summary>
    [Fact]
    public async Task A_full_retention_and_disposition_cycle_deletes_nothing()
    {
        var f = Fixture();
        var subjectId = await EligibleSubjectAsync(f);
        var hold = await ActiveHoldAsync(f, GlobalHold());
        await f.Holds.AddSubjectAsync(hold.Id, nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, Corr, CancellationToken.None);
        await f.Holds.ReleaseAsync(hold.Id, new ReleaseLegalHoldInput("LEGAL-REL-1", "GQD-1"), Corr, CancellationToken.None);

        var request = await DraftRequestAsync(f, subjectId);
        await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);
        await f.Dispositions.ApproveAsync(request, new ApproveDispositionInput("APPROVAL-1", null), Corr, CancellationToken.None);
        await f.Dispositions.ExecuteMarkerAsync(request, new ExecuteDispositionMarkerInput("EXEC-1"), Corr, CancellationToken.None);

        Assert.NotEmpty(f.PolicyRepo.Items);
        Assert.NotEmpty(f.SubjectRepo.Items);
        Assert.NotEmpty(f.HoldRepo.Items);
        Assert.NotEmpty(f.HoldSubjectRepo.Items);
        Assert.NotEmpty(f.DispositionRepo.Items);
        Assert.DoesNotContain(f.PolicyRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.SubjectRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.HoldRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.HoldSubjectRepo.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.DispositionRepo.Items, x => x.IsDeleted);
    }

    /// <summary>
    /// No FU15 repository contract exposes a delete/purge operation — the destruction path does not exist even as
    /// an accidental API surface.
    /// </summary>
    [Fact]
    public void No_retention_repository_contract_exposes_a_delete_or_purge_operation()
    {
        var contracts = new[]
        {
            typeof(IDocumentRetentionPolicyRepository), typeof(IDocumentRetentionSubjectRepository),
            typeof(IDocumentLegalHoldRepository), typeof(IDocumentLegalHoldSubjectRepository),
            typeof(IDocumentDispositionRequestRepository)
        };

        foreach (var contract in contracts)
        {
            Assert.DoesNotContain(contract.GetMethods(), m =>
                m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Purge", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Destroy", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>No FU15 aggregate can carry document content, so no regulated bytes reach these collections.</summary>
    [Fact]
    public void No_retention_aggregate_exposes_a_binary_content_property()
    {
        var types = new[]
        {
            typeof(DocumentRetentionPolicy), typeof(DocumentRetentionSubject), typeof(DocumentLegalHold),
            typeof(DocumentLegalHoldSubject), typeof(DocumentDispositionRequest)
        };

        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream) || p.PropertyType == typeof(Memory<byte>));
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> EligibleSubjectAsync(Harness f)
    {
        await ActivePolicyAsync(f, EvidencePolicy());
        var subjectId = Guid.NewGuid();
        await f.Evaluator.EvaluateAsync(EvidenceSubject(subjectId, DateTimeOffset.UtcNow.AddYears(-12)), Corr, CancellationToken.None);
        return subjectId;
    }

    private async Task<Guid> DraftRequestAsync(Harness f, Guid subjectId)
    {
        var r = await f.Dispositions.CreateAsync(
            new CreateDispositionRequestInput(nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, null),
            Corr, CancellationToken.None);
        return r.Data!.Id;
    }

    private async Task<Guid> SubmittedRequestAsync(Harness f)
    {
        var request = await DraftRequestAsync(f, await EligibleSubjectAsync(f));
        await f.Dispositions.SubmitAsync(request, Corr, CancellationToken.None);
        return request;
    }

    private async Task ActivePolicyAsync(Harness f, RetentionPolicyFieldsInput input)
    {
        var created = await f.Policies.CreateAsync(input, Corr, CancellationToken.None);
        await f.Policies.ActivateAsync(created.Data!.Id, Corr, CancellationToken.None);
    }

    private async Task<DocumentLegalHold> ActiveHoldAsync(Harness f, LegalHoldFieldsInput input)
    {
        var created = await f.Holds.CreateAsync(input, Corr, CancellationToken.None);
        await f.Holds.ActivateAsync(created.Data!.Id, new ActivateLegalHoldInput("LEGAL-APPROVAL-1", null, null), Corr, CancellationToken.None);
        return f.HoldRepo.Items.Single(x => x.Id == created.Data.Id);
    }

    private static RetentionPolicyFieldsInput TenYearPolicy() => new(
        PolicyKey: "CONTROLLED-DOC-10Y",
        PolicyName: "Controlled documents — effective plus 10 years after retirement/supersession",
        SubjectType: nameof(RetentionSubjectType.DocumentMasterRegisterEntry),
        RetentionClass: null,
        MinimumRetentionYears: 10,
        RetentionTrigger: nameof(RetentionTrigger.RetirementDate),
        RetainWhileEffective: true,
        RetainAfterRetirementYears: 10,
        RetainAfterSupersessionYears: 10,
        IsPermanentRetention: false,
        RegulatoryBasis: "GMG-QMS-SOP-0001 §22",
        Jurisdiction: "EU");

    private static RetentionPolicyFieldsInput EvidencePolicy() => new(
        PolicyKey: "APPROVAL-EVIDENCE-10Y",
        PolicyName: "Approval records — at least 10 years after completion",
        SubjectType: nameof(RetentionSubjectType.ApprovalEvidence),
        RetentionClass: null,
        MinimumRetentionYears: 10,
        RetentionTrigger: nameof(RetentionTrigger.CompletionDate),
        RetainWhileEffective: false,
        RetainAfterRetirementYears: null,
        RetainAfterSupersessionYears: null,
        IsPermanentRetention: false,
        RegulatoryBasis: "GMG-QMS-SOP-0001 §22",
        Jurisdiction: "EU");

    private static LegalHoldFieldsInput GlobalHold() => new(
        HoldTitle: "Regulatory inquiry 2026-07",
        HoldKey: null,
        HoldReason: nameof(LegalHoldReason.Litigation),
        ScopeType: nameof(LegalHoldScopeType.GlobalDocumentControl),
        RegisterEntryIds: null,
        ControlledDocumentIds: null,
        SubjectTypes: null,
        ExternalDocumentIds: null,
        ScopeDescription: "All document control records",
        IssuedByLegalUserId: null,
        IssuedByLegalRole: "General Counsel",
        EffectiveFrom: DateTimeOffset.UtcNow.AddDays(-1),
        EffectiveUntil: null);

    private static EvaluateRetentionInput EvidenceSubject(Guid subjectId, DateTimeOffset? triggerDate) =>
        new(nameof(RetentionSubjectType.ApprovalEvidence), subjectId, null, null, triggerDate, null);

    private static EvaluateRetentionInput RegisterSubject(Guid registerEntryId) =>
        new(nameof(RetentionSubjectType.DocumentMasterRegisterEntry), registerEntryId, registerEntryId, null, null, null);

    private static DocumentMasterRegisterEntry SeedRegisterEntry(
        Harness f, ControlledDocumentLifecycleStatus status, DateTimeOffset transitionAt)
    {
        var e = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop,
            DocumentType = DocumentType.Sop,
            Criticality = DocumentCriticality.Critical,
            IsControlledDocument = true,
            RegisterStatus = DocumentRegisterStatus.Active,
            LifecycleStatus = status,
            EffectiveDate = transitionAt.AddYears(-2),
            LastTransitionAt = transitionAt,
            PermanentUid = "UID-0000001",
            DocumentCode = "GMG-QMS-SOP-0001"
        };
        f.RegisterRepo.Items.Add(e);
        return e;
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var user = new FakeUser();

        var policyRepo = new FakePolicyRepo(tenant);
        var subjectRepo = new FakeSubjectRepo(tenant);
        var holdRepo = new FakeHoldRepo(tenant);
        var holdSubjectRepo = new FakeHoldSubjectRepo(tenant);
        var dispositionRepo = new FakeDispositionRepo(tenant);
        var registerRepo = new FakeRegisterRepo(tenant);

        var triggerResolver = new DocumentRetentionTriggerDateResolver(
            registerRepo, new FakePeriodicReviewRepo(), new FakeAllocationRepo(), new FakeExternalImpactRepo());
        var holdEvaluator = new DocumentLegalHoldEvaluator(holdRepo, holdSubjectRepo);
        var evaluator = new DocumentRetentionEvaluator(policyRepo, subjectRepo, triggerResolver, holdEvaluator, tenant, user);

        return new Harness(
            new DocumentRetentionPolicyService(policyRepo, tenant, user),
            evaluator,
            new DocumentLegalHoldService(holdRepo, holdSubjectRepo, tenant, user),
            new DocumentDispositionService(dispositionRepo, subjectRepo, holdEvaluator, tenant, user),
            policyRepo, subjectRepo, holdRepo, holdSubjectRepo, dispositionRepo, registerRepo);
    }

    private sealed record Harness(
        DocumentRetentionPolicyService Policies,
        DocumentRetentionEvaluator Evaluator,
        DocumentLegalHoldService Holds,
        DocumentDispositionService Dispositions,
        FakePolicyRepo PolicyRepo,
        FakeSubjectRepo SubjectRepo,
        FakeHoldRepo HoldRepo,
        FakeHoldSubjectRepo HoldSubjectRepo,
        FakeDispositionRepo DispositionRepo,
        FakeRegisterRepo RegisterRepo);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444415");
        public string? Email => "fu15@example.test";
        public string? DisplayName => "FU15 Tester";
        public string ActorName => "fu15@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakePolicyRepo(ITenantContext tenant) : IDocumentRetentionPolicyRepository
    {
        public List<DocumentRetentionPolicy> Items { get; } = [];
        private IEnumerable<DocumentRetentionPolicy> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRetentionPolicy> CreateAsync(DocumentRetentionPolicy p, CancellationToken ct = default) { Items.Add(p); return Task.FromResult(p); }
        public Task<DocumentRetentionPolicy?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentRetentionPolicy?> GetByKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PolicyKey == key));
        public Task<IReadOnlyList<DocumentRetentionPolicy>> GetActiveBySubjectTypeAsync(RetentionSubjectType t, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionPolicy>>(
                Scoped.Where(x => x.SubjectType == t && x.PolicyStatus == RetentionPolicyStatus.Active).ToList());
        public Task<IReadOnlyList<DocumentRetentionPolicy>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionPolicy>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRetentionPolicy p, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == p.Id);
            if (i >= 0) Items[i] = p;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeSubjectRepo(ITenantContext tenant) : IDocumentRetentionSubjectRepository
    {
        public List<DocumentRetentionSubject> Items { get; } = [];
        private IEnumerable<DocumentRetentionSubject> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRetentionSubject> CreateAsync(DocumentRetentionSubject s, CancellationToken ct = default) { Items.Add(s); return Task.FromResult(s); }
        public Task<DocumentRetentionSubject?> GetBySubjectAsync(RetentionSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.SubjectType == t && x.SubjectId == id));
        public Task<IReadOnlyList<DocumentRetentionSubject>> GetByRegisterEntryAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionSubject>>(Scoped.Where(x => x.RegisterEntryId == id).ToList());
        public Task<IReadOnlyList<DocumentRetentionSubject>> GetEligibleAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionSubject>>(
                Scoped.Where(x => x.IsDispositionEligible && !x.IsBlockedByLegalHold).ToList());
        public Task<IReadOnlyList<DocumentRetentionSubject>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionSubject>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRetentionSubject s, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == s.Id);
            if (i >= 0) Items[i] = s;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeHoldRepo(ITenantContext tenant) : IDocumentLegalHoldRepository
    {
        public List<DocumentLegalHold> Items { get; } = [];
        private IEnumerable<DocumentLegalHold> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentLegalHold> CreateAsync(DocumentLegalHold h, CancellationToken ct = default) { Items.Add(h); return Task.FromResult(h); }
        public Task<DocumentLegalHold?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentLegalHold>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHold>>(Scoped.Where(x => x.HoldStatus == LegalHoldStatus.Active).ToList());
        public Task<IReadOnlyList<DocumentLegalHold>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHold>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentLegalHold h, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == h.Id);
            if (i >= 0) Items[i] = h;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeHoldSubjectRepo(ITenantContext tenant) : IDocumentLegalHoldSubjectRepository
    {
        public List<DocumentLegalHoldSubject> Items { get; } = [];
        private IEnumerable<DocumentLegalHoldSubject> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentLegalHoldSubject> CreateAsync(DocumentLegalHoldSubject s, CancellationToken ct = default) { Items.Add(s); return Task.FromResult(s); }
        public Task<IReadOnlyList<DocumentLegalHoldSubject>> GetByHoldAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHoldSubject>>(Scoped.Where(x => x.LegalHoldId == id).ToList());
        public Task<IReadOnlyList<DocumentLegalHoldSubject>> GetBySubjectAsync(RetentionSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHoldSubject>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<bool> UpdateAsync(DocumentLegalHoldSubject s, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == s.Id);
            if (i >= 0) Items[i] = s;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeDispositionRepo(ITenantContext tenant) : IDocumentDispositionRequestRepository
    {
        public List<DocumentDispositionRequest> Items { get; } = [];
        private IEnumerable<DocumentDispositionRequest> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentDispositionRequest> CreateAsync(DocumentDispositionRequest r, CancellationToken ct = default) { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentDispositionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentDispositionRequest>> GetBySubjectAsync(RetentionSubjectType t, Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDispositionRequest>>(Scoped.Where(x => x.SubjectType == t && x.SubjectId == id).ToList());
        public Task<IReadOnlyList<DocumentDispositionRequest>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDispositionRequest>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentDispositionRequest r, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == r.Id);
            if (i >= 0) Items[i] = r;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }

    // Trigger-resolver collaborators the retention tests never exercise directly.
    private sealed class FakePeriodicReviewRepo : IDocumentPeriodicReviewRepository
    {
        public Task<DocumentPeriodicReview> CreateAsync(DocumentPeriodicReview r, CancellationToken ct = default) => Task.FromResult(r);
        public Task<DocumentPeriodicReview?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<DocumentPeriodicReview?>(null);
        public Task<IReadOnlyList<DocumentPeriodicReview>> GetByRegisterEntryAsync(Guid registerEntryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReview>>([]);
        public Task<DocumentPeriodicReview?> GetOpenAsync(Guid registerEntryId, CancellationToken ct = default) => Task.FromResult<DocumentPeriodicReview?>(null);
        public Task<bool> UpdateAsync(DocumentPeriodicReview r, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeAllocationRepo : IDocumentIdentifierAllocationRepository
    {
        public Task<DocumentIdentifierAllocation> CreateAsync(DocumentIdentifierAllocation a, CancellationToken ct = default) => Task.FromResult(a);
        public Task<DocumentIdentifierAllocation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<DocumentIdentifierAllocation?>(null);
        public Task<bool> ExistsValueIncludingDeletedAsync(DocumentIdentifierType type, string identifierValue, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<DocumentIdentifierAllocation>> ListAsync(IdentifierAllocationListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentIdentifierAllocation>>([]);
        public Task<bool> UpdateAsync(DocumentIdentifierAllocation a, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeExternalImpactRepo : IExternalDocumentImpactAssessmentRepository
    {
        public Task<ExternalDocumentImpactAssessment> CreateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default) => Task.FromResult(a);
        public Task<ExternalDocumentImpactAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ExternalDocumentImpactAssessment?>(null);
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetByExternalDocumentAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>([]);
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>([]);
        public Task<bool> UpdateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default) => Task.FromResult(true);
    }
}
