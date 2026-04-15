namespace Diten.Web.Models.DemandIdeas;

/// <summary>Shell for API-driven Capture page (data loaded via JavaScript).</summary>
public sealed class CaptureShellViewModel
{
    public string ApiBaseUrl { get; init; } = "";
    public string? InitialRecordId { get; init; }
}
