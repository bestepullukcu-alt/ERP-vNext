using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class GskuRetirementRequestWorkflowOptions
{
    public const string SectionName = "GskuRetirementRequestWorkflow";
    public bool Enabled { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public Guid[] CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = "GSKU_RETIREMENT_REQUEST";
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public int? DueAfterSeconds { get; set; }
    public GskuRetirementRequestStartConfiguration ToConfiguration(Guid? identityId, string? identityCode,
        Guid? correctionId, string? correctionCode)
    {
        if (!Enabled || (TemplateId.HasValue == !string.IsNullOrWhiteSpace(TemplateCode))
            || TemplateId == Guid.Empty || CandidatePrincipalIds.Length is < 1 or > 100
            || CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !Exact(ReasonCode, 128) || TemplateCode is not null && !Exact(TemplateCode, 128)
            || DueAfterSeconds is < 60 or > 2_592_000
            || TemplateId.HasValue && identityId == TemplateId
            || TemplateId.HasValue && correctionId == TemplateId
            || TemplateCode is not null && string.Equals(TemplateCode, identityCode,
                StringComparison.OrdinalIgnoreCase)
            || TemplateCode is not null && string.Equals(TemplateCode, correctionCode,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_CONFIGURATION_INVALID");
        return new(TemplateId, TemplateCode, CandidatePrincipalIds, ReasonCode, CommentRequired,
            EvidenceRequired, DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null,
            identityId, identityCode, correctionId, correctionCode);
    }
    private static bool Exact(string value, int max) => value.Length is > 0 && value.Length <= max
        && value == value.Trim() && !value.Any(char.IsControl);
}
