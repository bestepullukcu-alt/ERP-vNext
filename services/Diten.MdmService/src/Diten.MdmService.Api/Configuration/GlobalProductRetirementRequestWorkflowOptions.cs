using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class GlobalProductRetirementRequestWorkflowOptions
{
    public const string SectionName = "GlobalProductRetirementRequestWorkflow";
    public bool Enabled { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public Guid[] CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = "GLOBAL_PRODUCT_RETIREMENT";
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public int? DueAfterSeconds { get; set; }
    public GlobalProductRetirementRequestStartConfiguration ToConfiguration(Guid? identityId, string? identityCode,
        Guid? correctionId, string? correctionCode)
    {
        var retirement = (TemplateId, TemplateCode);
        var identity = (identityId, identityCode);
        var correction = (correctionId, correctionCode);
        if (!Enabled || (TemplateId.HasValue == !string.IsNullOrWhiteSpace(TemplateCode))
            || TemplateId == Guid.Empty || CandidatePrincipalIds is not { Length: >= 1 and <= 100 }
            || CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !Exact(ReasonCode, 128) || !ExactOptional(TemplateCode, 128)
            || !ExactOptional(identityCode, 128) || !ExactOptional(correctionCode, 128)
            || identityId == Guid.Empty || correctionId == Guid.Empty
            || DueAfterSeconds is < 60 or > 2_592_000
            || Same(retirement, identity) || Same(retirement, correction) || Same(identity, correction))
            throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_CONFIGURATION_INVALID");
        return new(TemplateId, TemplateCode, CandidatePrincipalIds.Order().ToArray(), ReasonCode,
            CommentRequired, EvidenceRequired,
            DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null,
            identityId, identityCode, correctionId, correctionCode);
    }

    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);
    private static bool ExactOptional(string? value, int max) => value is null || Exact(value, max);

    private static bool Same((Guid? Id, string? Code) a, (Guid? Id, string? Code) b) =>
        a.Id.HasValue && b.Id.HasValue && a.Id == b.Id
        || !string.IsNullOrWhiteSpace(a.Code) && !string.IsNullOrWhiteSpace(b.Code)
           && string.Equals(a.Code, b.Code, StringComparison.OrdinalIgnoreCase)
        || a.Id.HasValue && !string.IsNullOrWhiteSpace(b.Code)
        || !string.IsNullOrWhiteSpace(a.Code) && b.Id.HasValue;
}
