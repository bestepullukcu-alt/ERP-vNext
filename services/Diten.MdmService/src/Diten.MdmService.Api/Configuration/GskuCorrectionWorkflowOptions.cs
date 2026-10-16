using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class GskuCorrectionWorkflowOptions
{
    public const string SectionName = "GskuCorrectionWorkflow";
    public bool Enabled { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public Guid[] CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = "GSKU_CORRECTION";
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public int? DueAfterSeconds { get; set; }
    public GskuCorrectionStartConfiguration ToConfiguration(Guid? identityId, string? identityCode)
    {
        if (!Enabled || (TemplateId.HasValue == !string.IsNullOrWhiteSpace(TemplateCode))
            || TemplateId == Guid.Empty || CandidatePrincipalIds.Length is < 1 or > 100
            || CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !Exact(ReasonCode, 128) || TemplateCode is not null && !Exact(TemplateCode, 128)
            || DueAfterSeconds is < 60 or > 2_592_000
            || TemplateId.HasValue && identityId == TemplateId
            || TemplateCode is not null && string.Equals(TemplateCode, identityCode,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GSKU_CORRECTION_CONFIGURATION_INVALID");
        return new(TemplateId, TemplateCode, CandidatePrincipalIds, ReasonCode, CommentRequired,
            EvidenceRequired, DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null,
            identityId, identityCode);
    }
    private static bool Exact(string value, int max) => value.Length is > 0 && value.Length <= max
        && value == value.Trim() && !value.Any(char.IsControl);
}
