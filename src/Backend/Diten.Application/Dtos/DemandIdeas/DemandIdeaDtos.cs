namespace Diten.Application.Dtos.DemandIdeas;

public sealed class DemandIdeaResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string RecordNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProblemStatement { get; set; } = string.Empty;
    public string ExpectedOutcome { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string StrategicAlignment { get; set; } = string.Empty;
    public string BusinessUnit { get; set; } = string.Empty;
    public string Requestor { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public string ProposedScope { get; set; } = string.Empty;
    public string OutOfScope { get; set; } = string.Empty;
    public string Assumptions { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DemandSource { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ComplianceImpact { get; set; } = string.Empty;
    public string EstimatedComplexity { get; set; } = string.Empty;
    public string RiskSensitivity { get; set; } = string.Empty;
    public IReadOnlyList<string> SupportingLinks { get; set; } = Array.Empty<string>();
    public string Notes { get; set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AttachmentResponseDto> Attachments { get; set; } = Array.Empty<AttachmentResponseDto>();
    public IReadOnlyList<string> StrategicThemeKeys { get; set; } = Array.Empty<string>();
    /// <summary>Linked related demand/idea record IDs.</summary>
    public IReadOnlyList<string> RelatedIdeaIds { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = string.Empty;
    public DateTime? ReviewDueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class AttachmentResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}

public sealed class DemandIdeaUpsertRequest
{
    public string? Title { get; set; }
    public string? ProblemStatement { get; set; }
    public string? ExpectedOutcome { get; set; }
    public string? RequestType { get; set; }
    public string? StrategicAlignment { get; set; }
    public string? BusinessUnit { get; set; }
    public string? Requestor { get; set; }
    public string? Sponsor { get; set; }
    public string? OwnerName { get; set; }
    public string? ProposedScope { get; set; }
    public string? OutOfScope { get; set; }
    public string? Assumptions { get; set; }
    public string? Constraints { get; set; }
    public string? Category { get; set; }
    public string? DemandSource { get; set; }
    public string? Priority { get; set; }
    public string? ComplianceImpact { get; set; }
    public string? EstimatedComplexity { get; set; }
    public string? RiskSensitivity { get; set; }
    public List<string>? SupportingLinks { get; set; }
    public string? Notes { get; set; }
    public List<string>? Tags { get; set; }
    public List<AttachmentPayloadDto>? Attachments { get; set; }
    public List<string>? StrategicThemeKeys { get; set; }
    public List<string>? RelatedIdeaIds { get; set; }
    public DateTime? ReviewDueDate { get; set; }
}

public sealed class AttachmentPayloadDto
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;
}

public sealed class DemandIdeaMetadataDto
{
    public IReadOnlyList<string> RequestTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> BusinessUnits { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Categories { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DemandSources { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Priorities { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ComplianceImpacts { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> EstimatedComplexities { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RiskSensitivities { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> StrategicAlignments { get; set; } = Array.Empty<string>();
}

public sealed class RelatedIdeaItemDto
{
    public string Id { get; set; } = string.Empty;
    public string RecordNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int MatchScore { get; set; }
}

public sealed class DuplicateCheckRequest
{
    public string? Title { get; set; }
    public string? RequestType { get; set; }
    public string? BusinessUnit { get; set; }
    public List<string>? Tags { get; set; }
    public string? ExcludeId { get; set; }
}

public sealed class DuplicateIdeaItemDto
{
    public string Id { get; set; } = string.Empty;
    public string RecordNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Score { get; set; }
}

public sealed class RelatedQuery
{
    public string? Title { get; set; }
    public string? RequestType { get; set; }
    public string? BusinessUnit { get; set; }
    public string? StrategicAlignment { get; set; }
    public List<string>? Tags { get; set; }
    public string? ExcludeId { get; set; }
    public int Take { get; set; } = 5;
}

public sealed class StrategicThemeDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class ApiErrorEnvelope
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public Dictionary<string, List<string>>? Errors { get; set; }
}
