using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Contract;

/// <summary>
/// MOD-0167 FU02 contract surface: feature flags + in-domain vocabulary + supported filters + limits + reason codes +
/// permissions + limitations. Published so a contract-driven UI needs no hardcoded list anywhere.
/// <para>The flags that are <b>false</b> are as important as the ones that are true: materialised membership, a refresh
/// job, membership history, segment-of-segment, ICP scoring, computed attributes, usage logging, StrategyTemplate /
/// SubjectList / UCLN, CampaignTarget generation, frequency-policy writing, concept-graph authoring and a traversal
/// engine are all explicitly absent, so no consumer can assume a capability this FU does not have.</para>
/// </summary>
public sealed record SegmentContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    SegmentFeatureFlags Features,
    SegmentVocabularyDto Vocabularies,
    SegmentSupportedFilters SupportedFilters,
    SegmentContractLimits Limits,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);
