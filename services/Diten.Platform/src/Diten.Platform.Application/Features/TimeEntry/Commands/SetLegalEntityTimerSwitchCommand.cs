using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D12 — switch the timer on or off for one legal entity (on requires a reason). T1a stores it.</summary>
public sealed record SetLegalEntityTimerSwitchCommand(Guid LegalEntityId, SetLegalEntityTimerSwitchRequest Request, string CorrelationId)
    : IRequest<Response<LegalEntityTimeSettingDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Update, "LegalEntityTimeSetting",
        EntityId: LegalEntityId, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["timerEnabled"] = Request?.TimerEnabled, ["reason"] = Request?.Reason });
}
