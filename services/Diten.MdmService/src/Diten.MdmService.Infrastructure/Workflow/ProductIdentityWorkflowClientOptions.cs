namespace Diten.MdmService.Infrastructure.Workflow;

public sealed class ProductIdentityWorkflowClientOptions
{
    public const string SectionName = "ProductIdentityWorkflowClient";
    public string PlatformBaseUrl { get; init; } = string.Empty;
}
