namespace Diten.Web.Models.DemandIdeas;

public sealed class DemandIdeaDetailViewModel
{
    public string Id { get; init; } = "";
    public string RecordNumber { get; init; } = "";
    public string Title { get; init; } = "";
    public string Classification { get; init; } = "";
    public string RequestType { get; init; } = "";
    public string StrategicAlignment { get; init; } = "";
    public string Priority { get; init; } = "";
    public string Status { get; init; } = "";
    public string OwnerName { get; init; } = "";
    public string SponsorName { get; init; } = "";
    public string BusinessUnit { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime? SubmittedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReviewDueDate { get; init; }
    public IReadOnlyList<string> RelatedIdeaIds { get; init; } = Array.Empty<string>();
    public bool ShowTransferSection { get; init; }
    public string? TransferStatus { get; init; }
    public string? TransferTargetType { get; init; }
    public string? TransferTargetId { get; init; }
    public DateTime? TransferDate { get; init; }
    public string? TransferredBy { get; init; }
    public string? LinkedRecordUrl { get; init; }
}
