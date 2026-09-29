using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Commands;

/// <summary>MOD-0280-FU01 D6 — set the tenant's time-admin pool position (the last approver fallback).</summary>
public sealed record UpdateTimeEntrySettingsCommand(UpdateTimeEntrySettingsRequest Request, string CorrelationId)
    : IRequest<Response<TimeEntrySettingsDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        TimeEntryModule.AuditCategoryValue, AuditOperation.Update, "TimeEntrySettings",
        EntityId: null, SourceModule: TimeEntryModule.AuditSourceModule,
        CorrelationId: TimeEntryModule.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["timeAdminPoolPositionId"] = Request?.TimeAdminPoolPositionId });
}
