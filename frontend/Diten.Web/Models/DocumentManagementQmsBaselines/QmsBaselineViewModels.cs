using System.Text.Json.Serialization;

namespace Diten.Web.Models.DocumentManagementQmsBaselines;

/// <summary>
/// MOD-0028-FU03 — Gateway response envelope mirror, including the FU01/FU02 controlled-failure metadata
/// (<c>reason_code</c>, <c>correlation_id</c>) so the UI can render controlled errors and a support id.
/// </summary>
public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; set; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }
}

/// <summary>Baseline list/detail projection (mirrors FU02 QmsBaselineSummaryModel).</summary>
public sealed class QmsBaselineSummaryViewModel
{
    public Guid Id { get; set; }
    public string BaselineReleaseId { get; set; } = string.Empty;
    public string BaselineVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SnapshotHash { get; set; }
    public Guid? ManifestId { get; set; }
    public int DefinitionCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
