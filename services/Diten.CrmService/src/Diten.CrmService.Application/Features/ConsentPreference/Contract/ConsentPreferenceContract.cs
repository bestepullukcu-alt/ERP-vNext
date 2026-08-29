using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Domain.Entities;
using MediatR;
using PrefType = Diten.CrmService.Domain.Entities.PreferenceType;

namespace Diten.CrmService.Application.Features.ConsentPreference.Contract;

public sealed record GetConsentContractQuery : IRequest<Response<ConsentContractDto>>;

/// <summary>MOD-0164 FU02 contract surface (feature flags + supported vocabulary + permissions + limitations).</summary>
public sealed record ConsentContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    ConsentFeatureFlags Features,
    ConsentVocabulary Vocabulary,
    ConsentEvaluationVocabulary EvaluationVocabulary,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU02 capability flags. ONLY the consent/preference flags are present. The campaign-engine, visit-planning,
/// route-planning, digital-detailing, recommendation-engine and workflow-approval flags are deliberately ABSENT — and
/// never emitted as <c>false</c> either, because advertising a capability (even as false) would misrepresent the
/// boundary: MOD-0164 opens none of them.
/// </summary>
public sealed record ConsentFeatureFlags(
    bool SupportsConsentManagement,
    bool SupportsPreferenceManagement,
    bool SupportsConsentEvaluation,
    bool SupportsConsentPurposeChannelScope,
    bool SupportsConsentEvidenceReference,
    bool SupportsConsentFilterProvider)
{
    public static ConsentFeatureFlags Current => new(
        SupportsConsentManagement: true,
        SupportsPreferenceManagement: true,
        SupportsConsentEvaluation: true,
        SupportsConsentPurposeChannelScope: true,
        SupportsConsentEvidenceReference: true,
        SupportsConsentFilterProvider: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (surfaced so an authoring UI needs no hardcoded list).</summary>
public sealed record ConsentVocabulary(
    IReadOnlyList<string> SubjectTypes,
    IReadOnlyList<string> Channels,
    IReadOnlyList<string> PreferenceChannels,
    IReadOnlyList<string> Purposes,
    IReadOnlyList<string> LegalBases,
    IReadOnlyList<string> ConsentStatuses,
    IReadOnlyList<string> ScopeTypes,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> PreferenceTypes,
    IReadOnlyList<string> EvidenceRefTypes,
    IReadOnlyList<string> EvidenceSourceModules)
{
    public static ConsentVocabulary Current => new(
        ConsentSubjectType.All,
        ConsentChannel.All,
        PreferenceChannel.All,
        ConsentPurpose.All,
        ConsentLegalBasis.All,
        Domain.Entities.ConsentStatuses.All,
        ConsentScopeType.All,
        ConsentSource.All,
        PrefType.All,
        ConsentEvidenceRefType.All,
        ConsentEvidenceSourceModule.All);
}

/// <summary>The evaluation result vocabulary, so a consumer (MOD-0155 / MOD-0165 FU04 / MOD-0167) can be written
/// against the contract instead of against observed strings.</summary>
public sealed record ConsentEvaluationVocabulary(
    IReadOnlyList<string> EligibilityStatuses,
    IReadOnlyList<string> Decisions,
    string EvaluatorVersion)
{
    public static ConsentEvaluationVocabulary Current => new(
        new[]
        {
            ConsentEligibilityStatus.Allowed,
            ConsentEligibilityStatus.Blocked,
            ConsentEligibilityStatus.Unknown,
            ConsentEligibilityStatus.NotApplicable
        },
        new[]
        {
            ConsentDecision.ConsentGranted,
            ConsentDecision.ConsentBlocked,
            ConsentDecision.ConsentUnknown,
            ConsentDecision.PreferenceRestricted,
            ConsentDecision.NotApplicable
        },
        ConsentEvaluationResult.CurrentEvaluatorVersion);
}

public sealed class GetConsentContractHandler : IRequestHandler<GetConsentContractQuery, Response<ConsentContractDto>>
{
    public const string ModuleId = "MOD-0164";
    public const string ModuleName = "Consent & Preference Management";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU01-consent-preference-management-boundary; " +
        "FU02-consent-preference-runtime-evaluation-provider (authoring + read-only evaluation provider)";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "consent answers ONLY 'may this subject be reached on this channel for this purpose, in this scope, at this instant?' — never campaign targeting, visit/route planning, due/overdue, frequency, segmentation, digital detailing or content recommendation",
        "there is NO general consent flag: consent is always evaluated as subject x channel x purpose x scope x time, and a channel/purpose permission is never transferable",
        "the evaluate endpoint is GET/read-only: it performs no writes and returns no campaignTargetId / visitPlanId / routeId / dueStatus / lastVisitDate / frequency field",
        "no matching consent returns EligibilityStatus=unknown; unknown is NOT allowed and no default is invented",
        "an expired or out-of-window record is never allowed, but stays visible as a reason code",
        "a restrictive preference (do-not-contact / do-not-visit = true) blocks even a granted consent; an absent preference changes nothing",
        "frequency-cap preferences are advisory only (surfaced as preference_frequency_cap) — the frequency policy SoR stays MOD-0165 and no frequency runtime is opened here",
        "a scope-bound consent governs only its own scope: the general question never consumes a scoped record (reason consent_scope_mismatch)",
        "the provider never throws and never returns 500 to a consumer: an internal failure is a controlled unknown with reason consent_evaluation_error",
        "consent/preference vocabulary is validated in-domain (structural); MOD-0048 publish is out of FU02 scope",
        "EvidenceRef is validated at FORMAT level only — MOD-0164 stores a MOD-0028/MOD-0029 pointer and performs no document-master lookup, no file copy, no render and no evidence pack",
        "external references are stored and duplicate mappings are reported as conflicts; import/export is not implemented in FU02",
        "there is no DELETE; closing a record is a soft archive (ArchivedAt/By stamped) that stays readable but is excluded from evaluation",
        "the question dimensions (SubjectType/SubjectId, Channel, Purpose, ScopeType/ScopeId; and PreferenceType for preferences) are immutable after create — a different question is a different record",
        "consent/preference is never a flat field on Contact / AccountContactLink / Account; the MOD-0150 Contact 360 seam projection is not re-wired by FU02 (follow-up)",
        "MatchedPreferenceIds lists every preference applicable to the question at EffectiveAt; the blocking subset is identifiable via CandidatePreferences[].Restrictive",
        "EligibilityStatus=not_applicable and Decision=not_applicable are reserved contract values; the FU02 engine never emits them",
        "RBAC keys crm.consent.* / crm.preference.* are defined but NOT seeded; the endpoints run on the documented territory fallback (follow-up MOD-0164-FU-RBAC)",
        "UI is a follow-up (MOD-0164-FU03); FU02 ships the API + evaluation provider + tests"
    };

    private readonly ITenantContext _tenant;

    public GetConsentContractHandler(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task<Response<ConsentContractDto>> Handle(GetConsentContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<ConsentContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new ConsentContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true, // vocabulary is in-domain, so authoring is ready without a MOD-0048 publish
            ConsentFeatureFlags.Current,
            ConsentVocabulary.Current,
            ConsentEvaluationVocabulary.Current,
            ConsentPreferencePermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<ConsentContractDto>.Success(dto));
    }
}
