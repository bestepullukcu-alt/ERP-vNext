using System.Security.Cryptography;
using System.Text;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.Cycles;

public sealed class CreatePlanningCycleCommandHandler
    : IRequestHandler<CreatePlanningCycleCommand, Response<PlanningCycleResult>>
{
    private readonly IPlanningCycleAuthority _authority;
    private readonly IPlanningCycleStore _store;

    public CreatePlanningCycleCommandHandler(IPlanningCycleAuthority authority, IPlanningCycleStore store)
    {
        _authority = authority;
        _store = store;
    }

    public async Task<Response<PlanningCycleResult>> Handle(
        CreatePlanningCycleCommand command, CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty)
            return Response<PlanningCycleResult>.Fail("Tenant or actor scope is missing.", 403);
        if (command.SelectedLegalEntityHint == Guid.Empty ||
            command.AsOfDate == default || command.FirstWeekStart == default ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey) ||
            command.IdempotencyKey.Length > 128)
            return Response<PlanningCycleResult>.Fail("LegalEntity selection, dates and a valid idempotency key are required.");

        Guid? legalEntityId;
        AuthorizedPlanningContext? context;
        try
        {
            legalEntityId = await _authority.ResolveSelectedAsync(command.TenantId,
                command.ActorId, command.SelectedLegalEntityHint, cancellationToken);
            if (legalEntityId is null || legalEntityId == Guid.Empty)
                return Response<PlanningCycleResult>.Fail("LegalEntity scope was not found.", 404);
            if (legalEntityId.Value != command.SelectedLegalEntityHint)
                return Response<PlanningCycleResult>.Fail("LegalEntity assignment is inconsistent.", 503);
            context = await _authority.ResolveAsync(command.TenantId, legalEntityId.Value,
                command.AsOfDate, command.FirstWeekStart, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        {
            return Response<PlanningCycleResult>.Fail("Planning assignment or calendar source is unavailable.", 503);
        }
        if (context is null || context.LegalEntityId != legalEntityId)
            return Response<PlanningCycleResult>.Fail("Verified LegalEntity calendar is unavailable.", 503);
        if (string.IsNullOrWhiteSpace(context.CalendarId) ||
            string.IsNullOrWhiteSpace(context.CalendarVersion) ||
            string.IsNullOrWhiteSpace(context.TimeZoneId))
            return Response<PlanningCycleResult>.Fail("Calendar or LegalEntity time zone is unverified.", 503);
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(context.TimeZoneId); }
        catch (TimeZoneNotFoundException)
        {
            return Response<PlanningCycleResult>.Fail("LegalEntity time zone is invalid.", 422);
        }
        catch (InvalidTimeZoneException)
        {
            return Response<PlanningCycleResult>.Fail("LegalEntity time zone is invalid.", 422);
        }

        List<PlanningWeek> weeks;
        if (context.CalendarKind == PlanningCalendarKind.Iso8601Fallback)
        {
            if (command.FirstWeekStart.DayOfWeek != DayOfWeek.Monday ||
                context.CorporateWeeks is not null)
                return Response<PlanningCycleResult>.Fail("ISO week must start on Monday.", 422);
            weeks = Enumerable.Range(0, 52)
                .Select(i => new PlanningWeek
                {
                    Number = i + 1,
                    WeekStart = command.FirstWeekStart.AddDays(i * 7),
                    WeekEnd = command.FirstWeekStart.AddDays(i * 7 + 6)
                }).ToList();
        }
        else if (context.CalendarKind == PlanningCalendarKind.VerifiedCorporate &&
                 context.CorporateWeeks is { Count: 52 })
        {
            weeks = context.CorporateWeeks.Select(w => new PlanningWeek
            {
                Number = w.Number, WeekStart = w.WeekStart, WeekEnd = w.WeekEnd
            }).ToList();
        }
        else
            return Response<PlanningCycleResult>.Fail("A verified 52-week calendar is required.", 422);

        if (weeks.Count != 52 || weeks[0].WeekStart != command.FirstWeekStart ||
            weeks.Select((w, i) => w.Number != i + 1 ||
                w.WeekEnd != w.WeekStart.AddDays(6) ||
                (i > 0 && w.WeekStart != weeks[i - 1].WeekEnd.AddDays(1))).Any(invalid => invalid))
            return Response<PlanningCycleResult>.Fail("Calendar weeks are inconsistent.", 422);

        // The business period is the first local week-start date, independent of cycle,
        // as-of date and calendar version. No client-supplied period key is accepted.
        var fingerprintInput = $"{command.AsOfDate:yyyy-MM-dd}|{command.FirstWeekStart:yyyy-MM-dd}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
        var cycle = new PlanningCycle
        {
            TenantId = command.TenantId,
            LegalEntityId = legalEntityId.Value,
            CreatedByActorId = command.ActorId,
            AsOfDate = command.AsOfDate,
            CalendarId = context.CalendarId,
            CalendarVersion = context.CalendarVersion,
            TimeZoneId = context.TimeZoneId,
            HorizonStart = weeks[0].WeekStart,
            HorizonEnd = weeks[^1].WeekEnd,
            PlanningPeriodKey = weeks[0].WeekStart.ToString("yyyy-MM-dd"),
            Weeks = weeks,
            IdempotencyKey = command.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        CycleInsertResult result;
        try { result = await _store.InsertOrGetAsync(cycle, cancellationToken); }
        catch (InvalidOperationException)
        {
            return Response<PlanningCycleResult>.Fail("Planning cycle store is unavailable.", 503);
        }
        if (result.Outcome == CycleInsertOutcome.Conflict || result.Cycle is null)
            return Response<PlanningCycleResult>.Fail("Idempotency key was used for a different cycle request.", 409);
        var saved = result.Cycle;
        return Response<PlanningCycleResult>.Success(new PlanningCycleResult(
            saved.Id, saved.TenantId, saved.LegalEntityId, saved.AsOfDate,
            saved.CalendarId, saved.CalendarVersion, saved.TimeZoneId,
            saved.HorizonStart, saved.HorizonEnd, saved.PlanningPeriodKey,
            saved.Weeks.AsReadOnly()),
            result.Outcome == CycleInsertOutcome.Created ? 201 : 200);
    }
}
