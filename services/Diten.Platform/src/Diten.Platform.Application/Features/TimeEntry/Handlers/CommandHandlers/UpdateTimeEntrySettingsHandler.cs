using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D6 — the time-admin pool position, the approver of last resort. It must be a position of
/// this tenant (read through the org port). <c>ExpectedVersion</c> 0 creates the tenant's one settings row.</summary>
public sealed class UpdateTimeEntrySettingsHandler : IRequestHandler<UpdateTimeEntrySettingsCommand, Response<TimeEntrySettingsDto>>
{
    private readonly ITimeEntrySettingsRepository _settings;
    private readonly ITimeEntryOrgGateway _org;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly BackgroundJobSchedulerOptions _jobs;

    public UpdateTimeEntrySettingsHandler(
        ITimeEntrySettingsRepository settings,
        ITimeEntryOrgGateway org,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IOptions<BackgroundJobSchedulerOptions> jobs)
    {
        _jobs = jobs.Value;
        _settings = settings;
        _org = org;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TimeEntrySettingsDto>> Handle(UpdateTimeEntrySettingsCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await _settings.GetAsync(ct);

        // L5 — the pool is checked only when it CHANGES: a pool position archived after it was chosen must not block an
        // unrelated save (the reminder switch) with a 400. A changed pool must still be a live position of this tenant.
        if (request.Request.TimeAdminPoolPositionId is { } positionId
            && positionId != current?.TimeAdminPoolPositionId
            && (positionId == Guid.Empty || await _org.PositionAsync(positionId, ct) is not { IsArchived: false }))
        {
            return Response<TimeEntrySettingsDto>.Fail(
                "The time-admin pool must be a live position of this tenant.", 400,
                TimeEntryReasonCodes.SettingsPositionNotFound, request.CorrelationId);
        }

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
                new TimeEntrySettingsDto(current.TimeAdminPoolPositionId, current.Version, current.WeeklyReminderEnabled,
                    TimesheetReminderJob.IsScheduled(_jobs)), correlationId: request.CorrelationId)
            : Response<TimeEntrySettingsDto>.Fail(
                "The settings changed meanwhile; reload and retry.", 409,
                TimeEntryReasonCodes.SettingsConcurrencyConflict, request.CorrelationId);
    }
}
