using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class FirstGskuIdentityWorkflowOptions
{
    public const string SectionName = "FirstGskuIdentityWorkflow";

    public bool Enabled { get; init; }
    public Guid? TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public Guid[] CandidatePrincipalIds { get; init; } = [];
    public string ReasonCode { get; init; } = string.Empty;
    public bool CommentRequired { get; init; }
    public bool EvidenceRequired { get; init; }
    public int? DueAfterSeconds { get; init; }

    public FirstGskuIdentityWorkflowStartConfiguration ToStartConfiguration()
    {
        if (!Enabled
            || (TemplateId.HasValue == !string.IsNullOrEmpty(TemplateCode))
            || TemplateId == Guid.Empty
            || !ExactToken(TemplateCode, 128)
            || CandidatePrincipalIds is not { Length: >= 1 and <= 100 }
            || CandidatePrincipalIds.Any(id => id == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !ExactToken(ReasonCode, 128)
            || DueAfterSeconds is < 60 or > 2_592_000)
        {
            throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID");
        }

        return new(
            TemplateId,
            TemplateCode,
            CandidatePrincipalIds.Order().ToArray(),
            ReasonCode,
            CommentRequired,
            EvidenceRequired,
            DueAfterSeconds.HasValue ? TimeSpan.FromSeconds(DueAfterSeconds.Value) : null);
    }

    private static bool ExactToken(string? value, int maximum)
    {
        if (value is null)
        {
            return true;
        }

        return value.Length is > 0 && value.Length <= maximum
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(char.IsControl);
    }
}
