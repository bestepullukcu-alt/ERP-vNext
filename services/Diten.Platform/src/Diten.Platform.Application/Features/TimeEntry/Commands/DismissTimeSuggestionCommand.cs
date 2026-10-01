using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D8 — the person says "I did not attend": the suggestion is not offered again. Writes no time.</summary>
public sealed record DismissTimeSuggestionCommand(string WeekKey, Guid SuggestionId, string CorrelationId)
    : IRequest<Response<TimeSuggestionMutationDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Create, "TimeSuggestion",
        EntityId: SuggestionId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["decision"] = "dismiss", ["weekKey"] = WeekKey });
}
