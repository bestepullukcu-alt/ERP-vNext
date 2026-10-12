using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D7 (system) — take MOD-0023's outcome for a submitted week on board. Sent only when an
/// outcome is waiting, so every audit entry is an effective change.</summary>
public sealed record FinalizeTimesheetDecisionCommand(Guid WeekId, string CorrelationId)
    : IRequest<Response<string>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.LifecycleTransition, "TimesheetWeek",
        EntityId: WeekId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["transition"] = "finalize" });
}
