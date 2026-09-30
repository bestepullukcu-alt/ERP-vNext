using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D6 — the time-admin pool position, the approver of last resort. It must be a position of
/// this tenant (read through the org port). <c>ExpectedVersion</c> 0 creates the tenant's one settings row.</summary>
public sealed class UpdateTimeEntrySettingsHandler : IRequestHandler<UpdateTimeEntrySettingsCommand, Response<TimeEntrySettingsDto>>
{
    private readonly ITimeEntrySettingsRepository _settings;
    private readonly ITimeEntryOrgGateway _org;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public UpdateTimeEntrySettingsHandler(
        ITimeEntrySettingsRepository settings,
        ITimeEntryOrgGateway org,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _settings = settings;
        _org = org;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TimeEntrySettingsDto>> Handle(UpdateTimeEntrySettingsCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Request.TimeAdminPoolPositionId is { } positionId
            && (positionId == Guid.Empty || await _org.PositionAsync(positionId, ct) is not { IsArchived: false }))
        {
            return Response<TimeEntrySettingsDto>.Fail(
                "The time-admin pool must be a live position of this tenant.", 400,
                TimeEntryReasonCodes.SettingsPositionNotFound, request.CorrelationId);
        }

        var current = await _settings.GetAsync(ct);
        bool written;
        if (current is null)
        {
            current = new TimeEntrySettings
            {
                TenantId = _tenantContext.TenantId,
                TimeAdminPoolPositionId = request.Request.TimeAdminPoolPositionId,
                WeeklyReminderEnabled = request.Request.WeeklyReminderEnabled ?? false,
                CreatedBy = _currentUser.UserId.ToString()
            };
            written = request.Request.ExpectedVersion == 0 && await _settings.TryCreateAsync(current, ct);
        }
        else
        {
            current.TimeAdminPoolPositionId = request.Request.TimeAdminPoolPositionId;
            current.WeeklyReminderEnabled = request.Request.WeeklyReminderEnabled ?? current.WeeklyReminderEnabled;
            current.UpdatedBy = _currentUser.UserId.ToString();
            written = await _settings.UpdateAsync(current, request.Request.ExpectedVersion, ct);
        }

        return written
            ? Response<TimeEntrySettingsDto>.Success(
                new TimeEntrySettingsDto(current.TimeAdminPoolPositionId, current.Version, current.WeeklyReminderEnabled), correlationId: request.CorrelationId)
            : Response<TimeEntrySettingsDto>.Fail(
                "The settings changed meanwhile; reload and retry.", 409,
                TimeEntryReasonCodes.SettingsConcurrencyConflict, request.CorrelationId);
    }
}
