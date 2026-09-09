using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class LskuIdentityWorkflowOptions
{
    public const string SectionName = "LskuIdentityWorkflow";
    public bool Enabled { get; init; }
    public Guid? TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public Guid[] CandidatePrincipalIds { get; init; } = [];
    public string ReasonCode { get; init; } = string.Empty;
    public bool CommentRequired { get; init; }
    public bool EvidenceRequired { get; init; }
    public int? DueAfterSeconds { get; init; }

    public LskuIdentityWorkflowStartConfiguration ToStartConfiguration() => new(
        TemplateId, TemplateCode, CandidatePrincipalIds, ReasonCode,
        CommentRequired, EvidenceRequired,
        DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null);

    public bool IsValid() => !Enabled ||
        (TemplateId.HasValue ^ !string.IsNullOrWhiteSpace(TemplateCode))
        && CandidatePrincipalIds.Length is > 0 and <= 100
        && CandidatePrincipalIds.All(id => id != Guid.Empty)
        && CandidatePrincipalIds.Distinct().Count() == CandidatePrincipalIds.Length
        && !string.IsNullOrWhiteSpace(ReasonCode) && ReasonCode.Length <= 128
        && ReasonCode == ReasonCode.Trim() && !ReasonCode.Any(char.IsControl)
        && (!DueAfterSeconds.HasValue || DueAfterSeconds is >= 60 and <= 2_592_000);
}
