using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

namespace Diten.MdmService.Api.Configuration;

public sealed class ProductIdentityWorkflowOptions
{
    public const string SectionName = "ProductIdentityWorkflow";

    public bool Enabled { get; init; }
    public Guid? GlobalProductTemplateId { get; init; }
    public string? GlobalProductTemplateCode { get; init; }
    public Guid[] CandidatePrincipalIds { get; init; } = [];
    public string ReasonCode { get; init; } = string.Empty;
    public bool CommentRequired { get; init; }
    public bool EvidenceRequired { get; init; }
    public int? DueAfterSeconds { get; init; }

    public ProductIdentityWorkflowStartConfiguration ToStartConfiguration()
    {
        if (!Enabled
            || (GlobalProductTemplateId.HasValue == !string.IsNullOrEmpty(GlobalProductTemplateCode))
            || GlobalProductTemplateId == Guid.Empty
            || !ExactToken(GlobalProductTemplateCode, 128)
            || CandidatePrincipalIds is not { Length: >= 1 and <= 100 }
            || CandidatePrincipalIds.Any(id => id == Guid.Empty)
            || CandidatePrincipalIds.Distinct().Count() != CandidatePrincipalIds.Length
            || !ExactToken(ReasonCode, 128)
            || DueAfterSeconds is < 60 or > 2_592_000)
        {
            throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_CONFIGURATION_INVALID");
        }

        return new(
            GlobalProductTemplateId,
            GlobalProductTemplateCode,
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
