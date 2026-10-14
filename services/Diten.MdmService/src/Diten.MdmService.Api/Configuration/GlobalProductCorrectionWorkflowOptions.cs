using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class GlobalProductCorrectionWorkflowOptions
{
    public const string SectionName = "GlobalProductCorrectionWorkflow";
    public bool Enabled { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public Guid[] CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = "GLOBAL_PRODUCT_CORRECTION";
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public int? DueAfterSeconds { get; set; }

    public GlobalProductCorrectionStartConfiguration ToConfiguration(
        Guid? identityTemplateId,
        string? identityTemplateCode)
    {
        if (!Enabled) throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_CONFIGURATION_DISABLED");
        if ((TemplateId.HasValue == !string.IsNullOrWhiteSpace(TemplateCode))
            || TemplateId == Guid.Empty || CandidatePrincipalIds is not { Length: >= 1 and <= 100 }
            || CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !Exact(ReasonCode, 128) || !ExactOptional(TemplateCode, 128)
            || !ExactOptional(identityTemplateCode, 128) || identityTemplateId == Guid.Empty
            || DueAfterSeconds is < 60 or > 2_592_000
            || TemplateId.HasValue && !string.IsNullOrWhiteSpace(identityTemplateCode)
            || !string.IsNullOrWhiteSpace(TemplateCode) && identityTemplateId.HasValue
            || TemplateId.HasValue && identityTemplateId.HasValue && TemplateId == identityTemplateId
            || !string.IsNullOrWhiteSpace(TemplateCode)
               && !string.IsNullOrWhiteSpace(identityTemplateCode)
               && string.Equals(TemplateCode, identityTemplateCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_CONFIGURATION_INVALID");
        return new(TemplateId, TemplateCode, CandidatePrincipalIds.Order().ToArray(), ReasonCode,
            CommentRequired, EvidenceRequired,
            DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null,
            identityTemplateId, identityTemplateCode);
    }

    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    private static bool ExactOptional(string? value, int max) => value is null || Exact(value, max);
}
