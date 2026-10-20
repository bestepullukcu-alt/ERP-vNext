namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// Where the records go. All three are required; the service binds them from its own settings (CRM: <c>PlatformService:BaseUrl</c>
/// + <c>AuthService:InternalApiKey</c>). Unset is allowed — and LOUD (K8): a warning at start-up and one per record skipped.
/// </summary>
public sealed class PlatformAuditForwarderOptions
{
    /// <summary>Platform's own base address (the internal endpoint is not on the Gateway).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>The shared internal API key (<c>X-Internal-Api-Key</c>). Never logged.</summary>
    public string? InternalApiKey { get; set; }

    /// <summary>The record's <c>SourceService</c> (e.g. <c>Diten.CrmService</c>).</summary>
    public string? SourceService { get; set; }

    public bool IsConfigured => MissingSettings().Count == 0;

    /// <summary>The names of the unset settings (never their values).</summary>
    public IReadOnlyList<string> MissingSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            missing.Add(nameof(BaseUrl));
        }

        if (string.IsNullOrWhiteSpace(InternalApiKey))
        {
            missing.Add(nameof(InternalApiKey));
        }

        if (string.IsNullOrWhiteSpace(SourceService))
        {
            missing.Add(nameof(SourceService));
        }

        return missing;
    }
}
