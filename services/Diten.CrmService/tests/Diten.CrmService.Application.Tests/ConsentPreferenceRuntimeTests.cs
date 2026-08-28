using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ConsentPreference;
using Diten.CrmService.Application.Features.ConsentPreference.Commands;
using Diten.CrmService.Application.Features.ConsentPreference.Contract;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.ConsentPreference.Handlers;
using Diten.CrmService.Application.Features.ConsentPreference.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using PrefType = Diten.CrmService.Domain.Entities.PreferenceType;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0164 FU02 — Consent &amp; Preference runtime + read-only evaluation provider. Pins down: consent/preference are
/// their own aggregates (never flat fields on a subject master), TenantId is claim-only, the required question
/// dimensions, the effective-window rule, deterministic fail-closed resolution (scope specificity → restrictive status
/// → latest effective-from → stable id) with visible candidate diagnostics, "no consent ⇒ unknown and unknown is NOT
/// allowed", "a restrictive preference blocks even a granted consent", "an absent preference invents no default",
/// archive-excludes-from-evaluation-but-stays-readable, archived update ⇒ 409, no DELETE, and a write-free evaluate.
/// </summary>
public sealed class ConsentPreferenceRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Subject = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BrandX = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Mar1 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Dec1 = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeConsentRepo Consents { get; } = new();
        public FakePreferenceRepo Preferences { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public int TotalWrites => Consents.WriteCount + Preferences.WriteCount;

        public CreateConsentRecordHandler CreateConsent()
            => new(Tenant(TenantId), new NullActorContext(), Consents);

        public UpdateConsentRecordHandler UpdateConsent()
            => new(Tenant(TenantId), new NullActorContext(), Consents);

        public ArchiveConsentRecordHandler ArchiveConsent()
            => new(Tenant(TenantId), new NullActorContext(), Consents);

        public GetConsentRecordHandler GetConsent() => new(Tenant(TenantId), Consents);

        public ListConsentRecordsHandler ListConsents() => new(Tenant(TenantId), Consents);

        public CreatePreferenceRecordHandler CreatePreference()
            => new(Tenant(TenantId), new NullActorContext(), Preferences);

        public UpdatePreferenceRecordHandler UpdatePreference()
            => new(Tenant(TenantId), new NullActorContext(), Preferences);

        public ArchivePreferenceRecordHandler ArchivePreference()
            => new(Tenant(TenantId), new NullActorContext(), Preferences);

        public ListPreferenceRecordsHandler ListPreferences() => new(Tenant(TenantId), Preferences);

        public EvaluateConsentHandler Evaluate(Guid? tenant = null)
        {
            var ctx = Tenant(tenant ?? TenantId);
            return new EvaluateConsentHandler(ctx, new ConsentPreferenceEvaluator(ctx, Consents, Preferences));
        }
    }

    private static CreateConsentRecordCommand ConsentCmd(
        string subjectType = ConsentSubjectType.AccountContactLink,
        Guid? subjectId = null,
        string channel = ConsentChannel.Visit,
        string purpose = ConsentPurpose.MedicalVisit,
        string legalBasis = ConsentLegalBasis.ExplicitConsent,
        string status = ConsentStatuses.Granted,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string source = ConsentSource.FieldCapture,
        string? scopeType = null,
        Guid? scopeId = null,
        ConsentEvidenceRefInput? evidence = null,
        string? withdrawalReason = null,
        List<ConsentExternalReferenceInput>? externalReferences = null)
        => new(subjectType, subjectId ?? Subject, channel, purpose, legalBasis, status, from ?? Jan1, source,
            scopeType, scopeId, to, evidence, withdrawalReason, null, externalReferences);

    private static CreatePreferenceRecordCommand PreferenceCmd(
        string preferenceType = PrefType.DoNotVisit,
        string preferenceValue = "true",
        string channel = ConsentChannel.Visit,
        string subjectType = ConsentSubjectType.AccountContactLink,
        Guid? subjectId = null,
        int priority = 100,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string source = ConsentSource.SubjectDeclared,
        List<ConsentExternalReferenceInput>? externalReferences = null)
        => new(subjectType, subjectId ?? Subject, channel, preferenceType, preferenceValue, priority,
            from ?? Jan1, source, to, null, externalReferences);

    private static EvaluateConsentQuery EvalQuery(
        string channel = ConsentChannel.Visit,
        string purpose = ConsentPurpose.MedicalVisit,
        DateTimeOffset? at = null,
        string? scopeType = null,
        Guid? scopeId = null,
        Guid? subjectId = null,
        string subjectType = ConsentSubjectType.AccountContactLink)
        => new(subjectType, subjectId ?? Subject, channel, purpose, at ?? Mar1, scopeType, scopeId);

    private static ConsentRecord Seeded(
        Guid tenantId,
        string status,
        string channel = ConsentChannel.Visit,
        string purpose = ConsentPurpose.MedicalVisit,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? scopeType = null,
        Guid? scopeId = null,
        Guid? id = null,
        Guid? subjectId = null,
        string subjectType = ConsentSubjectType.AccountContactLink)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId,
            SubjectType = subjectType,
            SubjectId = subjectId ?? Subject,
            Channel = channel,
            Purpose = purpose,
            LegalBasis = ConsentLegalBasis.ExplicitConsent,
            ConsentStatus = status,
            EffectiveFrom = from ?? Jan1,
            EffectiveTo = to,
            Source = ConsentSource.FieldCapture,
            ScopeType = scopeType,
            ScopeId = scopeId
        };

    // ============ 1–8 · Authoring validation ============

    /// <summary>Test 1 — a valid granted consent persists with the claim tenant and normalized vocabulary.</summary>
    [Fact]
    public async Task T01_Create_Granted_Consent_Valid_Returns_201()
    {
        var f = new Fixture(TenantA);
        var r = await f.CreateConsent().Handle(ConsentCmd(), default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Consents.Items);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal("account-contact-link", row.SubjectType);
        Assert.Equal("visit", row.Channel);
        Assert.Equal("medical-visit", row.Purpose);
        Assert.Equal("granted", row.ConsentStatus);
        Assert.False(row.IsArchived());
    }

    /// <summary>Test 2 — TenantId can never arrive from a payload: no write contract exposes it, and a handler without
    /// a tenant claim refuses to write.</summary>
    [Fact]
    public async Task T02_TenantId_Is_Never_Accepted_From_Payload()
    {
        Type[] writeContracts =
        {
            typeof(CreateConsentRecordRequest), typeof(UpdateConsentRecordRequest),
            typeof(CreatePreferenceRecordRequest), typeof(UpdatePreferenceRecordRequest),
            typeof(CreateConsentRecordCommand), typeof(UpdateConsentRecordCommand),
            typeof(CreatePreferenceRecordCommand), typeof(UpdatePreferenceRecordCommand)
        };

        foreach (var contract in writeContracts)
        {
            Assert.DoesNotContain(
                contract.GetProperties().Select(p => p.Name),
                name => name.Equals("TenantId", StringComparison.OrdinalIgnoreCase));
        }

        var noTenant = new CreateConsentRecordHandler(
            new TenantContext(), new NullActorContext(), new FakeConsentRepo());
        var r = await noTenant.Handle(ConsentCmd(), default);
        Assert.Equal(400, r.StatusCode);
    }

    /// <summary>Test 3 — missing/unknown SubjectType ⇒ 400.</summary>
    [Fact]
    public async Task T03_Missing_SubjectType_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(subjectType: ""), default)).StatusCode);
        Assert.Empty(f.Consents.Items);
    }

    /// <summary>Test 4 — empty SubjectId ⇒ 400.</summary>
    [Fact]
    public async Task T04_Missing_SubjectId_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(subjectId: Guid.Empty), default)).StatusCode);
    }

    /// <summary>Test 5 — missing Channel ⇒ 400.</summary>
    [Fact]
    public async Task T05_Missing_Channel_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(channel: " "), default)).StatusCode);
    }

    /// <summary>Test 6 — missing Purpose, LegalBasis or ConsentStatus ⇒ 400 (all four are mandatory, none defaulted).</summary>
    [Fact]
    public async Task T06_Missing_Purpose_LegalBasis_Status_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(purpose: ""), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(legalBasis: ""), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(status: ""), default)).StatusCode);
        Assert.Empty(f.Consents.Items);
    }

    /// <summary>Test 7 — EffectiveTo &lt; EffectiveFrom ⇒ 400, for both aggregates.</summary>
    [Fact]
    public async Task T07_EffectiveTo_Before_EffectiveFrom_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(from: Jun1, to: Jan1), default)).StatusCode);
        Assert.Equal(400, (await f.CreatePreference().Handle(PreferenceCmd(from: Jun1, to: Jan1), default)).StatusCode);
    }

    /// <summary>Test 8 — an unknown channel / purpose / status / legal basis / preference type ⇒ 400. The vocabulary is
    /// in-domain, so a typo is rejected instead of silently stored and later evaluated as "unknown".</summary>
    [Fact]
    public async Task T08_Unknown_Vocabulary_Values_Return_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(channel: "carrier-pigeon"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(purpose: "gossip"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(status: "maybe"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(ConsentCmd(legalBasis: "vibes"), default)).StatusCode);
        Assert.Equal(400, (await f.CreatePreference().Handle(PreferenceCmd(preferenceType: "mood"), default)).StatusCode);
        Assert.Equal(400, (await f.CreatePreference().Handle(PreferenceCmd(channel: "telepathy"), default)).StatusCode);

        // A malformed restrictive value is rejected too: an ambiguous restriction must never be guessed at evaluation.
        Assert.Equal(400, (await f.CreatePreference().Handle(PreferenceCmd(preferenceValue: "sometimes"), default)).StatusCode);

        // A withdrawal without a reason is rejected: withdrawal history must stay explainable.
        Assert.Equal(400,
            (await f.CreateConsent().Handle(ConsentCmd(status: ConsentStatuses.Withdrawn), default)).StatusCode);
        Assert.Empty(f.Consents.Items);
    }

    // ============ 9–18 · Consent evaluation ============

    /// <summary>Test 9 — a matching granted consent ⇒ allowed / consent_granted.</summary>
    [Fact]
    public async Task T09_Evaluate_Matching_Granted_Returns_Allowed()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(201, (await f.CreateConsent().Handle(ConsentCmd(), default)).StatusCode);

        var r = await f.Evaluate().Handle(EvalQuery(), default);
        var result = r.Data!;

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ConsentEligibilityStatus.Allowed, result.EligibilityStatus);
        Assert.Equal(ConsentDecision.ConsentGranted, result.Decision);
        Assert.Contains(ConsentReasonCodes.ConsentGranted, result.ReasonCodes);
        Assert.NotNull(result.MatchedConsentId);
        Assert.Equal(ConsentEvaluationResult.CurrentEvaluatorVersion, result.EvaluatorVersion);
    }

    /// <summary>Test 10 — no matching consent ⇒ unknown + no_matching_consent. A different channel or purpose never
    /// satisfies the question (a permission is not transferable).</summary>
    [Fact]
    public async Task T10_Evaluate_No_Matching_Consent_Returns_Unknown()
    {
        var f = new Fixture(TenantA);
        // granted for e-mail/marketing only
        await f.CreateConsent().Handle(ConsentCmd(channel: ConsentChannel.Email, purpose: ConsentPurpose.Marketing), default);

        var visit = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, visit.EligibilityStatus);
        Assert.Equal(ConsentDecision.ConsentUnknown, visit.Decision);
        Assert.Contains(ConsentReasonCodes.NoMatchingConsent, visit.ReasonCodes);
        Assert.Null(visit.MatchedConsentId);

        // same channel, different purpose ⇒ still unknown
        var otherPurpose = (await f.Evaluate().Handle(
            EvalQuery(channel: ConsentChannel.Email, purpose: ConsentPurpose.Research), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, otherPurpose.EligibilityStatus);
    }

    /// <summary>Test 11 — unknown is never allowed: neither an absent record nor an explicitly authored
    /// <c>unknown</c> status produces <c>allowed</c>.</summary>
    [Fact]
    public async Task T11_Unknown_Is_Not_Treated_As_Allowed()
    {
        var f = new Fixture(TenantA);
        var empty = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.NotEqual(ConsentEligibilityStatus.Allowed, empty.EligibilityStatus);
        Assert.Equal(ConsentEligibilityStatus.Unknown, empty.EligibilityStatus);

        await f.CreateConsent().Handle(ConsentCmd(status: ConsentStatuses.Unknown), default);
        var authored = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, authored.EligibilityStatus);
        Assert.Equal(ConsentDecision.ConsentUnknown, authored.Decision);
        Assert.Contains(ConsentReasonCodes.ConsentUnknown, authored.ReasonCodes);
    }

    /// <summary>Test 12 — at equal scope specificity a denied record beats a granted one (fail-closed precedence), and
    /// the discriminator is visible.</summary>
    [Fact]
    public async Task T12_Denied_Beats_Granted_At_Same_Specificity()
    {
        var f = new Fixture(TenantA);
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted));
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Denied));

        var result = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Blocked, result.EligibilityStatus);
        Assert.Equal(ConsentDecision.ConsentBlocked, result.Decision);
        Assert.Contains(ConsentReasonCodes.ConsentDenied, result.ReasonCodes);
        Assert.Contains(ConsentReasonCodes.ConsentSelectedByRestrictiveStatus, result.ReasonCodes);
    }

    /// <summary>Test 13 — a withdrawn consent blocks (and the earlier granted record is not deleted, just outranked).</summary>
    [Fact]
    public async Task T13_Withdrawn_Blocks()
    {
        var f = new Fixture(TenantA);
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted));
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Withdrawn));

        var result = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Blocked, result.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentWithdrawn, result.ReasonCodes);
        Assert.Equal(2, f.Consents.Items.Count); // history preserved
    }

    /// <summary>Test 14 — a restricted consent blocks.</summary>
    [Fact]
    public async Task T14_Restricted_Blocks()
    {
        var f = new Fixture(TenantA);
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Restricted));

        var result = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Blocked, result.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentRestricted, result.ReasonCodes);
    }

    /// <summary>Test 15 — an expired record is never allowed, whether the window closed or the status says so, and the
    /// out-of-window / not-yet-effective reasons stay visible.</summary>
    [Fact]
    public async Task T15_Expired_And_OutOfWindow_Are_Not_Allowed()
    {
        var closedWindow = new Fixture(TenantA);
        closedWindow.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted, from: Jan1, to: Jan1.AddDays(10)));
        var expired = (await closedWindow.Evaluate().Handle(EvalQuery(at: Mar1), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, expired.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentExpired, expired.ReasonCodes);

        var authoredExpired = new Fixture(TenantA);
        authoredExpired.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Expired));
        var byStatus = (await authoredExpired.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.NotEqual(ConsentEligibilityStatus.Allowed, byStatus.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentExpired, byStatus.ReasonCodes);

        var future = new Fixture(TenantA);
        future.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted, from: Dec1));
        var notYet = (await future.Evaluate().Handle(EvalQuery(at: Mar1), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, notYet.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentNotEffective, notYet.ReasonCodes);
    }

    /// <summary>Test 16 — a scope-specific record governs its own scope and outranks the general record; and the
    /// general question never consumes a scoped record (consent_scope_mismatch).</summary>
    [Fact]
    public async Task T16_Scope_Specific_Consent_Beats_General_Consent()
    {
        var f = new Fixture(TenantA);
        var general = Seeded(TenantA, ConsentStatuses.Granted);
        var scoped = Seeded(TenantA, ConsentStatuses.Denied, scopeType: ConsentScopeType.Brand, scopeId: BrandX);
        f.Consents.Items.Add(general);
        f.Consents.Items.Add(scoped);

        var inScope = (await f.Evaluate().Handle(
            EvalQuery(scopeType: ConsentScopeType.Brand, scopeId: BrandX), default)).Data!;
        Assert.Equal(scoped.Id, inScope.MatchedConsentId);
        Assert.Equal(ConsentEligibilityStatus.Blocked, inScope.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentSelectedBySpecificity, inScope.ReasonCodes);

        var generalQuestion = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(general.Id, generalQuestion.MatchedConsentId);
        Assert.Contains(ConsentReasonCodes.ConsentScopeMismatch, generalQuestion.ReasonCodes);
    }

    /// <summary>Test 17 — at equal specificity and equal status precedence, the latest EffectiveFrom wins.</summary>
    [Fact]
    public async Task T17_Latest_EffectiveFrom_Tie_Break()
    {
        var f = new Fixture(TenantA);
        var older = Seeded(TenantA, ConsentStatuses.Granted, from: Jan1);
        var newer = Seeded(TenantA, ConsentStatuses.Granted, from: Mar1);
        f.Consents.Items.Add(older);
        f.Consents.Items.Add(newer);

        var result = (await f.Evaluate().Handle(EvalQuery(at: Jun1), default)).Data!;
        Assert.Equal(newer.Id, result.MatchedConsentId);
        Assert.Contains(ConsentReasonCodes.ConsentSelectedByLatestEffectiveFrom, result.ReasonCodes);
    }

    /// <summary>Test 18 — a full same-band tie is still resolved deterministically by the stable ConsentId, and the
    /// ambiguity is surfaced rather than hidden.</summary>
    [Fact]
    public async Task T18_Stable_ConsentId_Tie_Break_Is_Deterministic_And_Visible()
    {
        var lowId = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        var highId = Guid.Parse("00000000-0000-0000-0000-00000000000b");

        var f = new Fixture(TenantA);
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted, id: highId));
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted, id: lowId));

        var first = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        var second = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;

        Assert.Equal(lowId, first.MatchedConsentId);
        Assert.Equal(first.MatchedConsentId, second.MatchedConsentId);
        Assert.Contains(ConsentReasonCodes.ConsentAmbiguousConflict, first.ReasonCodes);
        Assert.Contains(ConsentReasonCodes.ConsentSelectedByStableId, first.ReasonCodes);
    }

    // ============ 19–21 · Preference overlay ============

    /// <summary>Test 19 — a do-not-visit preference blocks even a granted visit consent, and the granted consent stays
    /// visible as the matched record (the block is explained, not hidden).</summary>
    [Fact]
    public async Task T19_Preference_DoNotVisit_Blocks_Granted_Visit_Consent()
    {
        var f = new Fixture(TenantA);
        await f.CreateConsent().Handle(ConsentCmd(), default);
        var allowed = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Allowed, allowed.EligibilityStatus);

        Assert.Equal(201, (await f.CreatePreference().Handle(PreferenceCmd(), default)).StatusCode);

        var blocked = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Blocked, blocked.EligibilityStatus);
        Assert.Equal(ConsentDecision.PreferenceRestricted, blocked.Decision);
        Assert.Contains(ConsentReasonCodes.PreferenceDoNotVisit, blocked.ReasonCodes);
        Assert.Contains(ConsentReasonCodes.PreferenceRestricted, blocked.ReasonCodes);
        Assert.NotNull(blocked.MatchedConsentId);
        Assert.Single(blocked.MatchedPreferenceIds);
    }

    /// <summary>Test 20 — a do-not-contact preference blocks a communication consent; the <c>all</c> sentinel covers
    /// every channel, while a channel-scoped restriction stays inside its channel.</summary>
    [Fact]
    public async Task T20_Preference_DoNotContact_Blocks_Communication_Consent()
    {
        var f = new Fixture(TenantA);
        await f.CreateConsent().Handle(
            ConsentCmd(channel: ConsentChannel.Email, purpose: ConsentPurpose.Marketing), default);
        await f.CreateConsent().Handle(ConsentCmd(), default); // visit / medical-visit
        await f.CreatePreference().Handle(
            PreferenceCmd(preferenceType: PrefType.DoNotContact, channel: ConsentChannel.Email), default);

        var email = (await f.Evaluate().Handle(
            EvalQuery(channel: ConsentChannel.Email, purpose: ConsentPurpose.Marketing), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Blocked, email.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.PreferenceDoNotContact, email.ReasonCodes);
        Assert.Contains(ConsentReasonCodes.PreferenceChannelBlocked, email.ReasonCodes);

        // The e-mail restriction does not leak into the visit channel.
        var visit = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Allowed, visit.EligibilityStatus);

        // A blanket ('all') restriction does cover the visit channel.
        await f.CreatePreference().Handle(
            PreferenceCmd(preferenceType: PrefType.DoNotContact, channel: PreferenceChannel.AnyChannel), default);
        var blanket = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Blocked, blanket.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.PreferenceDoNotContact, blanket.ReasonCodes);
    }

    /// <summary>Test 21 — an absent preference invents no default, and a non-restrictive / false / not-yet-effective
    /// preference never blocks. A frequency-cap is advisory only (no frequency runtime is opened here).</summary>
    [Fact]
    public async Task T21_Absent_Or_NonRestrictive_Preference_Invents_No_Default()
    {
        var f = new Fixture(TenantA);
        await f.CreateConsent().Handle(ConsentCmd(), default);

        var noPreference = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Allowed, noPreference.EligibilityStatus);
        Assert.Empty(noPreference.MatchedPreferenceIds);

        await f.CreatePreference().Handle(PreferenceCmd(preferenceValue: "false"), default);
        await f.CreatePreference().Handle(
            PreferenceCmd(preferenceType: PrefType.PreferredChannel, preferenceValue: ConsentChannel.Email), default);
        await f.CreatePreference().Handle(
            PreferenceCmd(preferenceType: PrefType.FrequencyCap, preferenceValue: "2"), default);
        await f.CreatePreference().Handle(PreferenceCmd(from: Dec1), default); // not yet effective at Mar1

        var stillAllowed = (await f.Evaluate().Handle(EvalQuery(at: Mar1), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Allowed, stillAllowed.EligibilityStatus);
        Assert.Equal(ConsentDecision.ConsentGranted, stillAllowed.Decision);
        Assert.Contains(ConsentReasonCodes.PreferenceFrequencyCap, stillAllowed.ReasonCodes);
        Assert.Contains(ConsentReasonCodes.PreferenceNotEffective, stillAllowed.ReasonCodes);
        Assert.DoesNotContain(ConsentReasonCodes.PreferenceRestricted, stillAllowed.ReasonCodes);
    }

    // ============ 22–25 · Lifecycle · no delete · write-free evaluate ============

    /// <summary>Test 22 — an archived record is excluded from evaluation but stays readable (and the archived
    /// preference stops restricting).</summary>
    [Fact]
    public async Task T22_Archived_Excluded_From_Evaluate_But_Readable()
    {
        var f = new Fixture(TenantA);
        var consentId = (await f.CreateConsent().Handle(ConsentCmd(), default)).Data;
        var preferenceId = (await f.CreatePreference().Handle(PreferenceCmd(), default)).Data;

        Assert.Equal(ConsentEligibilityStatus.Blocked,
            (await f.Evaluate().Handle(EvalQuery(), default)).Data!.EligibilityStatus);

        Assert.Equal(200, (await f.ArchivePreference().Handle(new ArchivePreferenceRecordCommand(preferenceId), default)).StatusCode);
        Assert.Equal(ConsentEligibilityStatus.Allowed,
            (await f.Evaluate().Handle(EvalQuery(), default)).Data!.EligibilityStatus);

        Assert.Equal(200, (await f.ArchiveConsent().Handle(new ArchiveConsentRecordCommand(consentId), default)).StatusCode);
        var afterArchive = (await f.Evaluate().Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, afterArchive.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.NoMatchingConsent, afterArchive.ReasonCodes);

        // still readable as history
        var read = await f.GetConsent().Handle(new GetConsentRecordQuery(consentId), default);
        Assert.Equal(200, read.StatusCode);
        Assert.True(read.Data!.IsArchived);
        Assert.NotNull(read.Data!.ArchivedAt);

        var list = await f.ListConsents().Handle(new ListConsentRecordsQuery(), default);
        Assert.Single(list.Data!.Items);
        var withoutArchived = await f.ListConsents().Handle(new ListConsentRecordsQuery(IncludeArchived: false), default);
        Assert.Empty(withoutArchived.Data!.Items);
        var preferenceList = await f.ListPreferences().Handle(new ListPreferenceRecordsQuery(), default);
        Assert.Single(preferenceList.Data!.Items);
    }

    /// <summary>Test 23 — updating an archived record ⇒ 409 (archived records are read-only history).</summary>
    [Fact]
    public async Task T23_Archived_Update_Returns_409()
    {
        var f = new Fixture(TenantA);
        var consentId = (await f.CreateConsent().Handle(ConsentCmd(), default)).Data;
        var preferenceId = (await f.CreatePreference().Handle(PreferenceCmd(), default)).Data;
        await f.ArchiveConsent().Handle(new ArchiveConsentRecordCommand(consentId), default);
        await f.ArchivePreference().Handle(new ArchivePreferenceRecordCommand(preferenceId), default);

        var consentUpdate = await f.UpdateConsent().Handle(
            new UpdateConsentRecordCommand(consentId, ConsentLegalBasis.Contract, ConsentStatuses.Granted, Jan1,
                ConsentSource.Manual), default);
        Assert.Equal(409, consentUpdate.StatusCode);

        var preferenceUpdate = await f.UpdatePreference().Handle(
            new UpdatePreferenceRecordCommand(preferenceId, "false", 100, Jan1, ConsentSource.Manual), default);
        Assert.Equal(409, preferenceUpdate.StatusCode);
    }

    /// <summary>Test 24 — DELETE is unsupported: no controller action, command or repository method can delete a
    /// record, so a hard delete is structurally impossible rather than merely unrouted.</summary>
    [Fact]
    public void T24_Delete_Is_Structurally_Unsupported()
    {
        foreach (var controller in new[] { typeof(ConsentsController), typeof(PreferencesController) })
        {
            var deleteActions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpDeleteAttribute>().Any())
                .ToList();
            Assert.Empty(deleteActions);
        }

        foreach (var repository in new[] { typeof(IConsentRecordRepository), typeof(IPreferenceRecordRepository) })
        {
            Assert.DoesNotContain(
                repository.GetMethods().Select(m => m.Name),
                name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(
            typeof(CreateConsentRecordCommand).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(CreateConsentRecordCommand).Namespace)
                .Select(t => t.Name),
            name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Test 25 — evaluate is write-free: not a single repository write happens while evaluating, in the
    /// allowed, blocked and unknown branches alike.</summary>
    [Fact]
    public async Task T25_Evaluate_Is_Write_Free()
    {
        var f = new Fixture(TenantA);
        await f.CreateConsent().Handle(ConsentCmd(), default);
        await f.CreatePreference().Handle(PreferenceCmd(preferenceValue: "false"), default);
        var writesAfterAuthoring = f.TotalWrites;

        // From here on any repository write is a contract violation, not just an unexpected count.
        f.Consents.ReadOnlyMode = true;
        f.Preferences.ReadOnlyMode = true;

        await f.Evaluate().Handle(EvalQuery(), default);                                   // allowed
        await f.Evaluate().Handle(EvalQuery(channel: ConsentChannel.Sms), default);         // unknown
        f.Preferences.Items.Add(new PreferenceRecord
        {
            TenantId = TenantA, SubjectType = ConsentSubjectType.AccountContactLink, SubjectId = Subject,
            Channel = ConsentChannel.Visit, PreferenceType = PrefType.DoNotVisit, PreferenceValue = "true",
            Priority = 1, EffectiveFrom = Jan1, Source = ConsentSource.SubjectDeclared
        });
        await f.Evaluate().Handle(EvalQuery(), default);                                    // blocked

        Assert.Equal(writesAfterAuthoring, f.TotalWrites);
        Assert.Equal(0, f.Consents.ReadOnlyViolations);
        Assert.Equal(0, f.Preferences.ReadOnlyViolations);
    }

    // ============ 26–29 · Diagnostics · evidence · external references ============

    /// <summary>Test 26 — CandidateConsents diagnostics are visible: the winner, the outranked records and the
    /// eliminated ones each carry their reason.</summary>
    [Fact]
    public async Task T26_CandidateConsents_Diagnostics_Visible()
    {
        var f = new Fixture(TenantA);
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted, from: Jan1));
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Denied, from: Mar1));
        f.Consents.Items.Add(Seeded(TenantA, ConsentStatuses.Granted, from: Jan1, to: Jan1.AddDays(5)));

        var result = (await f.Evaluate().Handle(EvalQuery(at: Jun1), default)).Data!;

        Assert.Equal(3, result.CandidateConsents.Count);
        Assert.Single(result.CandidateConsents, c => c.Selected);
        Assert.All(result.CandidateConsents, c => Assert.False(string.IsNullOrWhiteSpace(c.Reason)));
        Assert.Contains(result.CandidateConsents, c => c.Reason == ConsentReasonCodes.ConsentExpired);
        Assert.False(string.IsNullOrWhiteSpace(result.SelectionReason));

        // Diagnostics can be switched off without changing the decision.
        var lean = ConsentEvaluationEngine.Evaluate(
            new ConsentEvaluationRequest(ConsentSubjectType.AccountContactLink, Subject, ConsentChannel.Visit,
                ConsentPurpose.MedicalVisit, Jun1, null, null, IncludeDiagnostics: false),
            f.Consents.Items, f.Preferences.Items, Jun1);
        Assert.Empty(lean.CandidateConsents);
        Assert.Equal(result.EligibilityStatus, lean.EligibilityStatus);
    }

    /// <summary>Test 27 — CandidatePreferences diagnostics are visible, with the restrictive flag and reason per row.</summary>
    [Fact]
    public async Task T27_CandidatePreferences_Diagnostics_Visible()
    {
        var f = new Fixture(TenantA);
        await f.CreateConsent().Handle(ConsentCmd(), default);
        await f.CreatePreference().Handle(PreferenceCmd(), default);                                  // restrictive
        await f.CreatePreference().Handle(PreferenceCmd(preferenceType: PrefType.FrequencyCap, preferenceValue: "3"), default);
        await f.CreatePreference().Handle(PreferenceCmd(from: Dec1), default);                        // not effective

        var result = (await f.Evaluate().Handle(EvalQuery(at: Mar1), default)).Data!;

        Assert.Equal(3, result.CandidatePreferences.Count);
        Assert.Single(result.CandidatePreferences, p => p.Restrictive);
        Assert.Contains(result.CandidatePreferences,
            p => p.Reason == ConsentReasonCodes.PreferenceFrequencyCap && !p.Restrictive);
        Assert.Contains(result.CandidatePreferences, p => p.Reason == ConsentReasonCodes.PreferenceNotEffective);
    }

    /// <summary>Test 28 — EvidenceRef is stored as a MOD-0028/MOD-0029 pointer and nothing else: no file content, no
    /// URL, no copy. A malformed pointer is rejected.</summary>
    [Fact]
    public async Task T28_EvidenceRef_Stored_As_Reference_Only()
    {
        var f = new Fixture(TenantA);
        var documentId = Guid.NewGuid();
        var created = await f.CreateConsent().Handle(
            ConsentCmd(evidence: new ConsentEvidenceRefInput(
                ConsentEvidenceRefType.Document, documentId, "MOD-0029", "DOC-2026-0001")), default);
        Assert.Equal(201, created.StatusCode);

        var dto = (await f.GetConsent().Handle(new GetConsentRecordQuery(created.Data), default)).Data!;
        Assert.NotNull(dto.EvidenceRef);
        Assert.Equal("document", dto.EvidenceRef!.RefType);
        Assert.Equal(documentId, dto.EvidenceRef.RefId);
        Assert.Equal("MOD-0029", dto.EvidenceRef.SourceModule);

        // The evidence pointer carries no payload/content/render surface.
        Assert.DoesNotContain(
            typeof(ConsentEvidenceRefDto).GetProperties().Select(p => p.Name),
            name => name.Contains("Content", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Url", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Uri", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Bytes", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("File", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(400, (await f.CreateConsent().Handle(
            ConsentCmd(evidence: new ConsentEvidenceRefInput("screenshot", documentId, "MOD-0029")), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(
            ConsentCmd(evidence: new ConsentEvidenceRefInput(ConsentEvidenceRefType.File, Guid.Empty, "MOD-0028")), default)).StatusCode);
        Assert.Equal(400, (await f.CreateConsent().Handle(
            ConsentCmd(evidence: new ConsentEvidenceRefInput(ConsentEvidenceRefType.File, documentId, "MOD-9999")), default)).StatusCode);
    }

    /// <summary>Test 29 — external references are stored with the full contract, and a duplicate mapping is a reported
    /// conflict (409) instead of a silent merge — in the payload and across records.</summary>
    [Fact]
    public async Task T29_ExternalReferences_Stored_And_Duplicates_Reported()
    {
        var f = new Fixture(TenantA);
        var created = await f.CreateConsent().Handle(
            ConsentCmd(externalReferences: new List<ConsentExternalReferenceInput>
            {
                new("OldCRM", "CONSENT-4711", "OPT-IN", "Legacy opt-in", Jan1, true)
            }), default);
        Assert.Equal(201, created.StatusCode);

        var dto = (await f.GetConsent().Handle(new GetConsentRecordQuery(created.Data), default)).Data!;
        var reference = Assert.Single(dto.ExternalReferences);
        Assert.Equal("OldCRM", reference.SourceSystem);
        Assert.Equal("CONSENT-4711", reference.ExternalId);
        Assert.Equal("OPT-IN", reference.ExternalCode);
        Assert.Equal("Legacy opt-in", reference.ExternalName);
        Assert.Equal(Jan1, reference.ImportedAt); // legacy history preserved, not rewritten
        Assert.True(reference.IsPrimary);

        // duplicate inside one payload ⇒ 409
        Assert.Equal(409, (await f.CreateConsent().Handle(
            ConsentCmd(externalReferences: new List<ConsentExternalReferenceInput>
            {
                new("OldCRM", "CONSENT-9000"), new("oldcrm", "CONSENT-9000")
            }), default)).StatusCode);

        // duplicate across records ⇒ 409 (no silent merge of two opt-in histories)
        Assert.Equal(409, (await f.CreateConsent().Handle(
            ConsentCmd(channel: ConsentChannel.Email, purpose: ConsentPurpose.Marketing,
                externalReferences: new List<ConsentExternalReferenceInput> { new("OldCRM", "CONSENT-4711") }),
            default)).StatusCode);

        // two primaries ⇒ 400
        Assert.Equal(400, (await f.CreateConsent().Handle(
            ConsentCmd(externalReferences: new List<ConsentExternalReferenceInput>
            {
                new("OldCRM", "A", IsPrimary: true), new("NewCRM", "B", IsPrimary: true)
            }), default)).StatusCode);

        // preferences carry the same contract
        var preference = await f.CreatePreference().Handle(
            PreferenceCmd(externalReferences: new List<ConsentExternalReferenceInput> { new("OldCRM", "PREF-1") }),
            default);
        Assert.Equal(201, preference.StatusCode);
        Assert.Single(f.Preferences.Items[0].ExternalReferences);
    }

    // ============ 30–32 · Contract flags · response shape ============

    /// <summary>Test 30 — the six FU02 contract flags are present and true, and the vocabulary is surfaced.</summary>
    [Fact]
    public async Task T30_Contract_Flags_Are_True()
    {
        var handler = new GetConsentContractHandler(Tenant(TenantA));
        var response = await handler.Handle(new GetConsentContractQuery(), default);
        var dto = response.Data!;

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("MOD-0164", dto.ModuleId);
        Assert.Equal(TenantA, dto.TenantId);
        Assert.True(dto.Features.SupportsConsentManagement);
        Assert.True(dto.Features.SupportsPreferenceManagement);
        Assert.True(dto.Features.SupportsConsentEvaluation);
        Assert.True(dto.Features.SupportsConsentPurposeChannelScope);
        Assert.True(dto.Features.SupportsConsentEvidenceReference);
        Assert.True(dto.Features.SupportsConsentFilterProvider);
        Assert.Equal(ConsentChannel.All, dto.Vocabulary.Channels);
        Assert.Equal(ConsentStatuses.All, dto.Vocabulary.ConsentStatuses);
        Assert.Contains(PreferenceChannel.AnyChannel, dto.Vocabulary.PreferenceChannels);
        Assert.Contains(ConsentEligibilityStatus.Unknown, dto.EvaluationVocabulary.EligibilityStatuses);
        Assert.NotEmpty(dto.Limitations);
        Assert.Equal(ConsentPreferencePermissions.All, dto.Permissions);
    }

    /// <summary>Test 31 — the forbidden capability flags are ABSENT from the contract (not even emitted as false).</summary>
    [Fact]
    public void T31_Forbidden_Contract_Flags_Are_Absent()
    {
        string[] forbidden =
        {
            "SupportsCampaignEngine", "SupportsVisitPlanning", "SupportsRoutePlanning",
            "SupportsDigitalDetailing", "SupportsRecommendationEngine", "SupportsWorkflowApproval"
        };

        var flagNames = typeof(ConsentFeatureFlags)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var flag in forbidden)
        {
            Assert.DoesNotContain(flag, flagNames);
        }

        // Exactly the six FU02 flags — nothing else is advertised, not even as false.
        Assert.Equal(6, flagNames.Count);
    }

    /// <summary>Test 32 — no campaign / visit / route / due / last-visit / frequency / content field appears anywhere in
    /// the FU02 response surface. MOD-0164 answers eligibility and nothing else.</summary>
    [Fact]
    public void T32_Response_Shape_Carries_No_Foreign_Domain_Fields()
    {
        string[] forbiddenFragments =
        {
            "campaigntarget", "campaignid", "visitplan", "route", "duestatus", "overdue", "lastvisit",
            "requiredvisitcount", "frequencypolicy", "knowledgecontent", "recommend", "segmentmembership",
            "workflow", "approval", "availability"
        };

        Type[] responseTypes =
        {
            typeof(ConsentEvaluationResult), typeof(CandidateConsent), typeof(CandidatePreference),
            typeof(ConsentRecordDto), typeof(PreferenceRecordDto), typeof(ConsentEvidenceRefDto),
            typeof(ConsentExternalReferenceDto)
        };

        foreach (var type in responseTypes)
        {
            foreach (var property in type.GetProperties())
            {
                var name = property.Name.ToLowerInvariant();
                Assert.DoesNotContain(forbiddenFragments, fragment => name.Contains(fragment));
            }
        }
    }

    // ============ 33–35 · Authorization · tenant isolation · determinism ============

    /// <summary>Test 33 — both controllers are [Authorize]-gated and every action carries a permission guard, so an
    /// unauthenticated Gateway call can only ever be 401 (never an anonymous consent read).</summary>
    [Fact]
    public void T33_Endpoints_Require_Authentication_And_Permission()
    {
        foreach (var controller in new[] { typeof(ConsentsController), typeof(PreferencesController) })
        {
            Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());

            var actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .ToList();

            Assert.NotEmpty(actions);
            foreach (var action in actions)
            {
                Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
                Assert.Contains(
                    action.GetCustomAttributes(),
                    a => a.GetType().Name.Contains("HasPermission", StringComparison.Ordinal));
            }
        }
    }

    /// <summary>Test 34 — tenant isolation: tenant B never reads, evaluates, updates or archives tenant A's records.</summary>
    [Fact]
    public async Task T34_Tenant_Isolation_Is_Enforced()
    {
        var f = new Fixture(TenantA);
        var consentId = (await f.CreateConsent().Handle(ConsentCmd(), default)).Data;
        await f.CreatePreference().Handle(PreferenceCmd(), default);

        // Same repository, tenant B context.
        var otherTenantRead = new GetConsentRecordHandler(Tenant(TenantB), f.Consents);
        Assert.Equal(404, (await otherTenantRead.Handle(new GetConsentRecordQuery(consentId), default)).StatusCode);

        var otherTenantList = new ListConsentRecordsHandler(Tenant(TenantB), f.Consents);
        Assert.Empty((await otherTenantList.Handle(new ListConsentRecordsQuery(), default)).Data!.Items);

        var otherTenantEvaluate = (await f.Evaluate(TenantB).Handle(EvalQuery(), default)).Data!;
        Assert.Equal(ConsentEligibilityStatus.Unknown, otherTenantEvaluate.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.NoMatchingConsent, otherTenantEvaluate.ReasonCodes);

        var otherTenantUpdate = new UpdateConsentRecordHandler(Tenant(TenantB), new NullActorContext(), f.Consents);
        Assert.Equal(404, (await otherTenantUpdate.Handle(
            new UpdateConsentRecordCommand(consentId, ConsentLegalBasis.Contract, ConsentStatuses.Denied, Jan1,
                ConsentSource.Manual), default)).StatusCode);

        var otherTenantArchive = new ArchiveConsentRecordHandler(Tenant(TenantB), new NullActorContext(), f.Consents);
        Assert.Equal(404, (await otherTenantArchive.Handle(new ArchiveConsentRecordCommand(consentId), default)).StatusCode);
    }

    /// <summary>Test 35 — the provider degrades in a controlled way: a repository failure yields <c>unknown</c> with the
    /// error reason code instead of a 500, and unknown is still not allowed. Also pins the immutability of the question
    /// dimensions on update.</summary>
    [Fact]
    public async Task T35_Provider_Never_500s_And_Question_Dimensions_Are_Immutable()
    {
        var throwing = new ThrowingConsentRepo();
        var context = Tenant(TenantA);
        var evaluator = new ConsentPreferenceEvaluator(context, throwing, new FakePreferenceRepo());
        var handler = new EvaluateConsentHandler(context, evaluator);

        var response = await handler.Handle(EvalQuery(), default);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(ConsentEligibilityStatus.Unknown, response.Data!.EligibilityStatus);
        Assert.Contains(ConsentReasonCodes.ConsentEvaluationError, response.Data!.ReasonCodes);
        Assert.NotEqual(ConsentEligibilityStatus.Allowed, response.Data!.EligibilityStatus);

        // Update cannot repurpose a record to a different question.
        foreach (var contract in new[] { typeof(UpdateConsentRecordCommand), typeof(UpdateConsentRecordRequest) })
        {
            var names = contract.GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("SubjectType", names);
            Assert.DoesNotContain("SubjectId", names);
            Assert.DoesNotContain("Channel", names);
            Assert.DoesNotContain("Purpose", names);
            Assert.DoesNotContain("ScopeType", names);
            Assert.DoesNotContain("ScopeId", names);
        }

        foreach (var contract in new[] { typeof(UpdatePreferenceRecordCommand), typeof(UpdatePreferenceRecordRequest) })
        {
            var names = contract.GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("SubjectType", names);
            Assert.DoesNotContain("SubjectId", names);
            Assert.DoesNotContain("Channel", names);
            Assert.DoesNotContain("PreferenceType", names);
        }

        // A status transition (withdrawal) IS allowed and is audit stamped — it never deletes the record.
        var f = new Fixture(TenantA);
        var consentId = (await f.CreateConsent().Handle(ConsentCmd(), default)).Data;
        var withdraw = await f.UpdateConsent().Handle(
            new UpdateConsentRecordCommand(consentId, ConsentLegalBasis.ExplicitConsent, ConsentStatuses.Withdrawn,
                Jan1, ConsentSource.SubjectDeclared, WithdrawalReason: "Subject requested removal"), default);
        Assert.Equal(200, withdraw.StatusCode);
        var stored = Assert.Single(f.Consents.Items);
        Assert.Equal("withdrawn", stored.ConsentStatus);
        Assert.Equal("Subject requested removal", stored.WithdrawalReason);
        Assert.NotNull(stored.UpdatedAt);
        Assert.Equal(ConsentEligibilityStatus.Blocked,
            (await f.Evaluate().Handle(EvalQuery(), default)).Data!.EligibilityStatus);
    }

    // ---------------- Fakes ----------------

    private sealed class FakeConsentRepo : IConsentRecordRepository
    {
        public List<ConsentRecord> Items { get; } = new();
        public int WriteCount { get; private set; }

        /// <summary>Incremented if a write is attempted from a read/evaluate path — always expected to stay 0.</summary>
        public int ReadOnlyViolations { get; private set; }

        public bool ReadOnlyMode { get; set; }

        public Task<ConsentRecord?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));

        public Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConsentRecord>)Items
                .Where(r => r.TenantId == t && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt).ToList());

        public Task<IReadOnlyList<ConsentRecord>> ListForEvaluationAsync(
            Guid t, string subjectType, Guid subjectId, string channel, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<ConsentRecord>)Items
                .Where(r => r.TenantId == t && !r.IsDeleted && !r.IsArchived()
                            && r.SubjectType == subjectType && r.SubjectId == subjectId && r.Channel == channel)
                .ToList());

        public Task<ConsentRecord?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r =>
                r.TenantId == t && !r.IsDeleted && !r.IsArchived()
                && r.ExternalReferences.Any(x =>
                    string.Equals(x.SourceSystem, sourceSystem, StringComparison.OrdinalIgnoreCase)
                    && x.ExternalId == externalId)));

        public Task InsertAsync(ConsentRecord record, CancellationToken ct)
        {
            if (ReadOnlyMode) ReadOnlyViolations++;
            WriteCount++;
            Items.Add(record);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ConsentRecord record, CancellationToken ct)
        {
            if (ReadOnlyMode) ReadOnlyViolations++;
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePreferenceRepo : IPreferenceRecordRepository
    {
        public List<PreferenceRecord> Items { get; } = new();
        public int WriteCount { get; private set; }
        public int ReadOnlyViolations { get; private set; }
        public bool ReadOnlyMode { get; set; }

        public Task<PreferenceRecord?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));

        public Task<IReadOnlyList<PreferenceRecord>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<PreferenceRecord>)Items
                .Where(r => r.TenantId == t && !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt).ToList());

        public Task<IReadOnlyList<PreferenceRecord>> ListForEvaluationAsync(
            Guid t, string subjectType, Guid subjectId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<PreferenceRecord>)Items
                .Where(r => r.TenantId == t && !r.IsDeleted && !r.IsArchived()
                            && r.SubjectType == subjectType && r.SubjectId == subjectId)
                .ToList());

        public Task<PreferenceRecord?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r =>
                r.TenantId == t && !r.IsDeleted && !r.IsArchived()
                && r.ExternalReferences.Any(x =>
                    string.Equals(x.SourceSystem, sourceSystem, StringComparison.OrdinalIgnoreCase)
                    && x.ExternalId == externalId)));

        public Task InsertAsync(PreferenceRecord record, CancellationToken ct)
        {
            if (ReadOnlyMode) ReadOnlyViolations++;
            WriteCount++;
            Items.Add(record);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PreferenceRecord record, CancellationToken ct)
        {
            if (ReadOnlyMode) ReadOnlyViolations++;
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Simulates an infrastructure failure during evaluation (the provider must degrade, not throw).</summary>
    private sealed class ThrowingConsentRepo : IConsentRecordRepository
    {
        public Task<ConsentRecord?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => throw new InvalidOperationException("consent store unavailable");

        public Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid t, CancellationToken ct)
            => throw new InvalidOperationException("consent store unavailable");

        public Task<IReadOnlyList<ConsentRecord>> ListForEvaluationAsync(
            Guid t, string subjectType, Guid subjectId, string channel, CancellationToken ct)
            => throw new InvalidOperationException("consent store unavailable");

        public Task<ConsentRecord?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct)
            => throw new InvalidOperationException("consent store unavailable");

        public Task InsertAsync(ConsentRecord record, CancellationToken ct)
            => throw new InvalidOperationException("consent store unavailable");

        public Task UpdateAsync(ConsentRecord record, CancellationToken ct)
            => throw new InvalidOperationException("consent store unavailable");
    }
}
