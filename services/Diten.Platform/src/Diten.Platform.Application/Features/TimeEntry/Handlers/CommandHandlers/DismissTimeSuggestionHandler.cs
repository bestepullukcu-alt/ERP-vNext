using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D8 — "I did not attend": the decision is stored so the suggestion is not offered again. No
/// time is written, and no week is touched.</summary>
public sealed class DismissTimeSuggestionHandler : IRequestHandler<DismissTimeSuggestionCommand, Response<TimeSuggestionMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimeSuggestionReader _suggestions;
    private readonly ITimeSuggestionRepository _decisions;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public DismissTimeSuggestionHandler(
        ITimesheetWeekReader reader,
        ITimeSuggestionReader suggestions,
        ITimeSuggestionRepository decisions,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _reader = reader;
        _suggestions = suggestions;
        _decisions = decisions;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Response<TimeSuggestionMutationDto>> Handle(DismissTimeSuggestionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Response<TimeSuggestionMutationDto>.Fail(
                "Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request.CorrelationId);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);
        var suggestion = (await _suggestions.ListForWeekAsync(context, ct))
            .FirstOrDefault(s => s.Id == request.SuggestionId && s.Offered);
        if (suggestion is null)
        {
            return Response<TimeSuggestionMutationDto>.Fail(
                "Suggestion not found.", 404, TimeEntryReasonCodes.SuggestionNotFound, request.CorrelationId);
        }

        var written = suggestion.Decision is null && await _decisions.TryCreateAsync(new TimeSuggestion
        {
            Id = suggestion.Id,
            TenantId = _tenantContext.TenantId,
            UserId = userId,
            MeetingId = suggestion.Invitation.MeetingId,
            LocalDate = suggestion.LocalDate,
            ProposedMinutes = suggestion.ProposedMinutes,
            State = TimeSuggestionState.Dismissed,
            DecidedAtUtc = _clock.GetUtcNow(),
            CreatedBy = userId.ToString()
        }, ct);

        return written
            ? Response<TimeSuggestionMutationDto>.Success(
                new TimeSuggestionMutationDto(suggestion.Id, nameof(TimeSuggestionState.Dismissed), null, null),
                correlationId: request.CorrelationId)
            : Response<TimeSuggestionMutationDto>.Fail(
                "This suggestion was already decided.", 409, TimeEntryReasonCodes.SuggestionAlreadyDecided, request.CorrelationId);
    }
}
