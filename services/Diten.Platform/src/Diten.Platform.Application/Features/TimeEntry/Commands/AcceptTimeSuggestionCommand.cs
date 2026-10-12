using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D8 — the person says "I attended": the meeting's proposed minutes become a draft row
/// (<c>Source = Meeting</c>) in the week's open draft.</summary>
public sealed record AcceptTimeSuggestionCommand(string WeekKey, Guid SuggestionId, AcceptTimeSuggestionRequest Request, string CorrelationId)
    : IRequest<Response<TimeSuggestionMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Create, "TimeSuggestion",
        EntityId: SuggestionId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["decision"] = "accept", ["weekKey"] = WeekKey });
}
