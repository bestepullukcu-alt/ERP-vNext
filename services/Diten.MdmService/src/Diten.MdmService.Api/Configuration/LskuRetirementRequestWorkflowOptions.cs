using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class LskuRetirementRequestWorkflowOptions
{
    public const string SectionName = "LskuRetirementRequestWorkflow";
    public bool Enabled { get; set; }
    public Guid? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public Guid[] CandidatePrincipalIds { get; set; } = [];
    public string ReasonCode { get; set; } = "LSKU_RETIREMENT";
    public bool CommentRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public int? DueAfterSeconds { get; set; }

    public LskuRetirementRequestStartConfiguration ToConfiguration(Guid? identityId, string? identityCode)
    {
        if (!Enabled || (TemplateId.HasValue == !string.IsNullOrWhiteSpace(TemplateCode))
            || TemplateId == Guid.Empty || CandidatePrincipalIds is not { Length: >= 1 and <= 100 }
            || CandidatePrincipalIds.Any(x => x == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !Exact(ReasonCode, 128) || !ExactOptional(TemplateCode, 128)
            || identityId == Guid.Empty || Same((TemplateId, TemplateCode), (identityId, identityCode))
            || DueAfterSeconds is < 60 or > 2_592_000)
            throw new InvalidOperationException("LSKU_RETIREMENT_CONFIGURATION_INVALID");
        return new(TemplateId, TemplateCode, CandidatePrincipalIds.Order().ToArray(), ReasonCode,
            CommentRequired, EvidenceRequired,
            DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null);
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
