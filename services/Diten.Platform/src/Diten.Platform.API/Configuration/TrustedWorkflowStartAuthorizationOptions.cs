namespace Diten.Platform.API.Configuration;

public sealed class TrustedWorkflowStartAuthorizationOptions
{
    public const string SectionName = "TrustedWorkflowStartAuthorization";
    public const int MaximumEntries = 64;

    public List<TrustedWorkflowStartAuthorizationEntry> Entries { get; set; } = [];
}

public sealed class TrustedWorkflowStartAuthorizationEntry
{
    public Guid ClientId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
}
