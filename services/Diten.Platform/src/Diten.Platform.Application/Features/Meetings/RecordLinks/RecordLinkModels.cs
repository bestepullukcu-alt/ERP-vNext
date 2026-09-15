namespace Diten.Platform.Application.Features.Meetings.RecordLinks;

/// <summary>One side of a <c>RecordLink</c> — a module code and the id it owns on that side.</summary>
public readonly record struct RecordLinkEndpoint(string ModuleCode, Guid RecordId);

/// <summary>What a resolver hands back for one id: the two fields `relatedRecords[]` needs beyond id/type.</summary>
public sealed record RelatedRecordSummary(string Title, string Link);
