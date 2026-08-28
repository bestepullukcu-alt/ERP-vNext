using System.Globalization;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkingCalendar;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.WorkingCalendarImport;

public sealed class StartWorkingCalendarImportHandler : IRequestHandler<StartWorkingCalendarImportCommand, Response<Guid>>
{
    private readonly IWorkingCalendarRepository _calendars;
    private readonly IWorkingCalendarImportBatchRepository _batches;
    private readonly IHolidayProvider _provider;
    private readonly WorkingCalendarImportOptions _options;

    public StartWorkingCalendarImportHandler(IWorkingCalendarRepository calendars,
        IWorkingCalendarImportBatchRepository batches, IHolidayProvider provider,
        IOptions<WorkingCalendarImportOptions> options)
        => (_calendars, _batches, _provider, _options) = (calendars, batches, provider, options.Value);

    public async Task<Response<Guid>> Handle(StartWorkingCalendarImportCommand request, CancellationToken ct)
    {
        if (!_options.Enabled)
            return Response<Guid>.Fail("Holiday auto-fetch is disabled.", 403, "auto_fetch_disabled");
        if (WorkingCalendarImportActors.IsSystem(request.RequestedBy)
            && !string.Equals(request.RequestedBy, WorkingCalendarImportActors.Scheduler, StringComparison.Ordinal))
            return Response<Guid>.Fail("Unsupported system maker.", 403, "system_actor_forbidden");
        if (!WorkingCalendarImportTriggerSource.All.Contains(request.TriggerSource))
            return Response<Guid>.Fail("Unsupported trigger source.", 400);
        if (!string.IsNullOrWhiteSpace(request.ScheduledRunKey))
        {
            var existing = await _batches.GetByScheduledRunKeyAsync(request.ScheduledRunKey, ct);
            if (existing is not null && (WorkingCalendarImportStatus.Open.Contains(existing.ImportStatus)
                || existing.ImportStatus == WorkingCalendarImportStatus.Applied))
                return Response<Guid>.Success(existing.Id);
        }

        var target = await _calendars.GetCountryLayerByIdAsync(request.TargetCalendarId, ct);
        if (target is null) return Response<Guid>.Fail("Target country calendar was not found.", 404);
        if (await _batches.HasOpenBatchAsync(target.Id, ct))
            return Response<Guid>.Fail("An open import batch already exists for this target.", 409, "open_batch_exists");

        var batch = new WorkingCalendarImportBatch
        {
            BatchCode = $"WCI-{target.CountryCode}-{target.CalendarYear}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
            CountryCode = target.CountryCode,
            CalendarYear = target.CalendarYear,
            TargetCalendarId = target.Id,
            TargetCalendarCodeSnapshot = target.CalendarCode,
            IncludeNonPublicTypes = request.IncludeNonPublicTypes,
            ProviderKey = _provider.ProviderKey,
            TriggerSource = request.TriggerSource,
            RequestedBy = request.RequestedBy,
            RequestedAt = DateTimeOffset.UtcNow,
            ScheduledRunKey = request.ScheduledRunKey,
            Notes = request.Notes?.Trim(),
            CreatedBy = request.RequestedBy
        };
        await _batches.CreateAsync(batch, ct); // staging exists before the external fetch starts
        try
        {
            var fetched = await _provider.FetchAsync(target.CountryCode, target.CalendarYear, ct);
            batch.ProviderEndpoint = fetched.Endpoint;
            batch.ProviderFetchedAt = fetched.FetchedAt;
            batch.ProviderPayloadHash = fetched.PayloadHash;
            batch.ProviderOutcome = fetched.Outcome;
            batch.Candidates = MapCandidates(fetched.Holidays, target, batch, request.IncludeNonPublicTypes,
                out var skipped, out var duplicateRows);
            batch.SkippedNonPublicCount = skipped;
            batch.DuplicateSourceRowCount = duplicateRows;
            batch.ImportStatus = WorkingCalendarImportStatus.PendingReview;
            batch.RecalculateCounts();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            batch.ImportStatus = WorkingCalendarImportStatus.Failed;
            batch.ProviderOutcome = HolidayProviderOutcome.Failed;
            batch.FailureReason = ex.GetType().Name;
        }
        await _batches.ReplaceAsync(batch, 1, ct);
        return batch.ImportStatus == WorkingCalendarImportStatus.Failed
            ? Response<Guid>.Fail("Holiday provider fetch failed; no calendar data was changed.", 503, "holiday_provider_unavailable")
            : Response<Guid>.Success(batch.Id, 201);
    }

    private static List<WorkingCalendarImportCandidate> MapCandidates(IReadOnlyList<ProviderHoliday> holidays,
        Domain.Entities.WorkingCalendar.WorkingCalendar calendar, WorkingCalendarImportBatch batch,
        bool includeNonPublic, out int skipped, out int duplicateRows)
    {
        skipped = 0; duplicateRows = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<WorkingCalendarImportCandidate>();
        foreach (var holiday in holidays)
        {
            var isPublic = holiday.Types.Any(x => string.Equals(x, "Public", StringComparison.OrdinalIgnoreCase));
            if (!isPublic && !includeNonPublic) { skipped++; continue; }
            if (!seen.Add(holiday.ProviderRef)) { duplicateRows++; continue; }
            var flags = new List<string>();
            if (!isPublic) flags.Add(WorkingCalendarImportFlags.TypeNotPublic);
            if (!holiday.IsNationwide || holiday.Subdivisions?.Count > 0) flags.Add(WorkingCalendarImportFlags.SubdivisionScoped);
            if (holiday.Date.Year != calendar.CalendarYear) flags.Add(WorkingCalendarImportFlags.DateOutsideCalendarYear);
            var code = BuildCode(holiday, calendar.CountryCode);
            var sameDate = calendar.ActiveDays().FirstOrDefault(x => x.EffectiveDate == holiday.Date);
            var sameRef = calendar.ActiveDays().FirstOrDefault(x => x.ProviderRef == holiday.ProviderRef);
            var codeCollision = calendar.ActiveDays().Any(x => string.Equals(x.DayCode, code, StringComparison.OrdinalIgnoreCase));
            if (sameDate is { Source: WorkingCalendarSource.Manual }) flags.Add(WorkingCalendarImportFlags.ExistingManualDay);
            if (codeCollision && sameRef is null) flags.Add(WorkingCalendarImportFlags.DayCodeCollision);
            if (!isPublic) flags.Add(WorkingCalendarImportFlags.UnmappedType);
            var change = sameRef is not null ? WorkingCalendarImportChangeKind.AlreadyPresent
                : sameDate is { Source: WorkingCalendarSource.Manual } ? WorkingCalendarImportChangeKind.ConflictsManual
                : WorkingCalendarImportChangeKind.New;
            result.Add(new WorkingCalendarImportCandidate
            {
                ProviderDayKey = holiday.ProviderRef,
                Date = holiday.Date,
                ProviderName = holiday.Name,
                ProviderLocalName = holiday.LocalName,
                ProviderTypes = holiday.Types.ToList(),
                ProviderIsNationwide = holiday.IsNationwide,
                ProviderSubdivisions = holiday.Subdivisions?.ToList(),
                MappedDayType = isPublic ? WorkingCalendarDayType.PublicHoliday : null,
                MappedDayCode = code,
                MappedDayName = string.IsNullOrWhiteSpace(holiday.LocalName) ? holiday.Name : holiday.LocalName,
                ChangeKind = change,
                ExistingDayId = sameRef?.DayId ?? sameDate?.DayId,
                Flags = flags
            });
        }
        return result;
    }

    private static string BuildCode(ProviderHoliday holiday, string countryCode)
    {
        var normalized = holiday.Name.Normalize(NormalizationForm.FormD);
        var slug = new string(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        if (slug.Length > 32) slug = slug[..32].TrimEnd('-');
        return $"{countryCode}-{holiday.Date:yyyyMMdd}-{slug}";
    }
}

public sealed class DecideWorkingCalendarImportCandidateHandler : IRequestHandler<DecideWorkingCalendarImportCandidateCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarImportBatchRepository _batches;
    public DecideWorkingCalendarImportCandidateHandler(IWorkingCalendarImportBatchRepository batches) => _batches = batches;
    public async Task<Response<NoContent>> Handle(DecideWorkingCalendarImportCandidateCommand request, CancellationToken ct)
    {
        if (WorkingCalendarImportActors.IsSystem(request.Actor)) return Response<NoContent>.Fail("System actors cannot review imports.", 403);
        var batch = await _batches.GetByIdAsync(request.BatchId, ct);
        if (batch is null) return Response<NoContent>.Fail("Import batch was not found.", 404);
        var candidate = batch.Candidates.FirstOrDefault(x => x.CandidateId == request.CandidateId);
        if (candidate is null) return Response<NoContent>.Fail("Import candidate was not found.", 404);
        var guard = Decide(candidate, request.Decision, request.Reason, request.Actor);
        if (guard is not null) return guard;
        batch.ImportStatus = WorkingCalendarImportStatus.InReview; batch.RecalculateCounts();
        return await _batches.ReplaceAsync(batch, batch.Version, ct) ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("The import batch changed; reload it.", 409);
    }
    internal static Response<NoContent>? Decide(WorkingCalendarImportCandidate candidate, string decision, string? reason, string actor)
    {
        if (!WorkingCalendarImportDecision.All.Contains(decision)) return Response<NoContent>.Fail("Unsupported decision.", 400);
        if (decision == WorkingCalendarImportDecision.Approved && (candidate.MappedDayType is null || candidate.Flags.Count > 0))
            return Response<NoContent>.Fail("Flagged or unmapped candidates cannot be approved.", 400);
        candidate.Decision = decision; candidate.DecisionReason = reason?.Trim(); candidate.DecidedBy = actor;
        candidate.DecidedAt = DateTimeOffset.UtcNow; return null;
    }
}

public sealed class DecideWorkingCalendarImportBatchHandler : IRequestHandler<DecideWorkingCalendarImportBatchCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarImportBatchRepository _batches;
    public DecideWorkingCalendarImportBatchHandler(IWorkingCalendarImportBatchRepository batches) => _batches = batches;
    public async Task<Response<NoContent>> Handle(DecideWorkingCalendarImportBatchCommand request, CancellationToken ct)
    {
        if (WorkingCalendarImportActors.IsSystem(request.Actor)) return Response<NoContent>.Fail("System actors cannot review imports.", 403);
        var batch = await _batches.GetByIdAsync(request.BatchId, ct);
        if (batch is null) return Response<NoContent>.Fail("Import batch was not found.", 404);
        foreach (var input in request.Decisions)
        {
            var candidate = batch.Candidates.FirstOrDefault(x => x.CandidateId == input.CandidateId);
            if (candidate is null) return Response<NoContent>.Fail("Import candidate was not found.", 404);
            var guard = DecideWorkingCalendarImportCandidateHandler.Decide(candidate, input.Decision, input.Reason, request.Actor);
            if (guard is not null) return guard;
        }
        batch.ImportStatus = WorkingCalendarImportStatus.InReview; batch.RecalculateCounts();
        return await _batches.ReplaceAsync(batch, batch.Version, ct) ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("The import batch changed; reload it.", 409);
    }
}

public sealed class ApplyWorkingCalendarImportHandler : IRequestHandler<ApplyWorkingCalendarImportCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarRepository _calendars; private readonly IWorkingCalendarImportBatchRepository _batches;
    public ApplyWorkingCalendarImportHandler(IWorkingCalendarRepository calendars, IWorkingCalendarImportBatchRepository batches)
        => (_calendars, _batches) = (calendars, batches);
    public async Task<Response<NoContent>> Handle(ApplyWorkingCalendarImportCommand request, CancellationToken ct)
    {
        if (!request.HasApplyPermission || WorkingCalendarImportActors.IsSystem(request.Actor)) return Response<NoContent>.Fail("Apply is forbidden.", 403);
        var batch = await _batches.GetByIdAsync(request.BatchId, ct);
        if (batch is null) return Response<NoContent>.Fail("Import batch was not found.", 404);
        if (batch.RequestedBy == request.Actor) return Response<NoContent>.Fail("Maker and checker must be different actors.", 403, "segregation_of_duties");
        if (!WorkingCalendarImportStatus.Open.Contains(batch.ImportStatus) || batch.UndecidedCount > 0)
            return Response<NoContent>.Fail("Every candidate must be reviewed before apply.", 409);
        var calendar = await _calendars.GetCountryLayerByIdAsync(batch.TargetCalendarId, ct);
        if (calendar is null) return Response<NoContent>.Fail("Target calendar was not found.", 404);
        if (calendar.IsActive() && !request.HasActivatePermission) return Response<NoContent>.Fail("Applying to an active calendar requires activate permission.", 403);
        var approved = batch.Candidates.Where(x => x.Decision == WorkingCalendarImportDecision.Approved).ToList();
        var newDays = new List<WorkingCalendarDay>();
        foreach (var candidate in approved)
        {
            var input = new WorkingCalendarDayInput(null, candidate.MappedDayCode, candidate.MappedDayName,
                candidate.Date, null, WorkingCalendarDayType.PublicHoliday, WorkingCalendarRecurrence.None, false, null);
            var guard = WorkingCalendarValidation.ValidateDayInput(calendar, input, null);
            if (!guard.Ok) return Response<NoContent>.Fail(guard.Message!, guard.StatusCode, guard.ReasonCode);
            var day = new WorkingCalendarDay { DayCode = input.DayCode, DayName = input.DayName, Date = input.Date,
                ObservedDate = null, DayType = input.DayType, Recurrence = WorkingCalendarRecurrence.None,
                IsHalfDay = false, Source = WorkingCalendarSource.ProviderFetch, ProviderBatchId = batch.Id,
                ProviderRef = candidate.ProviderDayKey, CreatedBy = request.Actor };
            calendar.Days.Add(day); newDays.Add(day); candidate.AppliedDayId = day.DayId;
        }
        calendar.Source = WorkingCalendarSource.ProviderFetch; calendar.UpdatedBy = request.Actor;
        if (!await _calendars.ReplaceAsync(calendar, request.ExpectedCalendarVersion, ct))
            return Response<NoContent>.Fail("The target calendar changed; nothing was applied.", 409);
        batch.ImportStatus = WorkingCalendarImportStatus.Applied; batch.AppliedBy = request.Actor; batch.AppliedAt = DateTimeOffset.UtcNow;
        batch.AppliedDayIds = newDays.Select(x => x.DayId).ToList(); batch.TargetCalendarVersionAtApply = calendar.Version;
        if (!await _batches.ReplaceAsync(batch, request.ExpectedBatchVersion, ct))
            return Response<NoContent>.Fail("Calendar was applied but batch finalization conflicted.", 409, "batch_finalize_conflict");
        return Response<NoContent>.Success(204);
    }
}

public sealed class DiscardWorkingCalendarImportHandler : IRequestHandler<DiscardWorkingCalendarImportCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarImportBatchRepository _batches;
    public DiscardWorkingCalendarImportHandler(IWorkingCalendarImportBatchRepository batches) => _batches = batches;
    public async Task<Response<NoContent>> Handle(DiscardWorkingCalendarImportCommand request, CancellationToken ct)
    {
        if (WorkingCalendarImportActors.IsSystem(request.Actor)) return Response<NoContent>.Fail("System actors cannot discard imports.", 403);
        var batch = await _batches.GetByIdAsync(request.BatchId, ct);
        if (batch is null) return Response<NoContent>.Fail("Import batch was not found.", 404);
        if (!WorkingCalendarImportStatus.Open.Contains(batch.ImportStatus)) return Response<NoContent>.Fail("Only open imports can be discarded.", 409);
        batch.ImportStatus = WorkingCalendarImportStatus.Discarded; batch.FailureReason = request.Reason?.Trim(); batch.UpdatedBy = request.Actor;
        return await _batches.ReplaceAsync(batch, request.ExpectedVersion, ct) ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("The import batch changed; reload it.", 409);
    }
}

public sealed class WorkingCalendarImportQueryHandlers :
    IRequestHandler<GetWorkingCalendarImportContractQuery, Response<WorkingCalendarImportContractDto>>,
    IRequestHandler<ListWorkingCalendarImportsQuery, Response<IReadOnlyList<WorkingCalendarImportBatchDto>>>,
    IRequestHandler<GetWorkingCalendarImportByIdQuery, Response<WorkingCalendarImportBatchDto>>,
    IRequestHandler<GetWorkingCalendarImportProviderStatusQuery, Response<HolidayProviderStatusDto>>,
    IRequestHandler<GetWorkingCalendarImportScheduleQuery, Response<HolidayAutoFetchScheduleDto>>
{
    private readonly IWorkingCalendarImportBatchRepository _batches; private readonly WorkingCalendarImportOptions _options;
    public WorkingCalendarImportQueryHandlers(IWorkingCalendarImportBatchRepository batches, IOptions<WorkingCalendarImportOptions> options)
        => (_batches, _options) = (batches, options.Value);
    public Task<Response<WorkingCalendarImportContractDto>> Handle(GetWorkingCalendarImportContractQuery request, CancellationToken ct)
        => Task.FromResult(Response<WorkingCalendarImportContractDto>.Success(new WorkingCalendarImportContractDto(WorkingCalendarImportStatus.All,
            WorkingCalendarImportDecision.All, WorkingCalendarImportChangeKind.All, WorkingCalendarImportFlags.All,
            WorkingCalendarImportTriggerSource.All,
            new[] { WorkingCalendarImportPermissionKeys.Read, WorkingCalendarImportPermissionKeys.Run,
                WorkingCalendarImportPermissionKeys.Review, WorkingCalendarImportPermissionKeys.Apply },
            "one aggregate ReplaceAsync(expectedVersion); never automatic", "ObservedDate is always null")));
    public async Task<Response<IReadOnlyList<WorkingCalendarImportBatchDto>>> Handle(ListWorkingCalendarImportsQuery request, CancellationToken ct)
        => Response<IReadOnlyList<WorkingCalendarImportBatchDto>>.Success((await _batches.ListAsync(request.Status,
            request.CountryCode, request.CalendarYear, request.TargetCalendarId, request.TriggerSource, ct)).Select(Map).ToList());
    public async Task<Response<WorkingCalendarImportBatchDto>> Handle(GetWorkingCalendarImportByIdQuery request, CancellationToken ct)
    { var batch = await _batches.GetByIdAsync(request.Id, ct); return batch is null ? Response<WorkingCalendarImportBatchDto>.Fail("Import batch was not found.", 404) : Response<WorkingCalendarImportBatchDto>.Success(Map(batch)); }
    public Task<Response<HolidayProviderStatusDto>> Handle(GetWorkingCalendarImportProviderStatusQuery request, CancellationToken ct)
    { var host = Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var uri) ? uri.Host : string.Empty; return Task.FromResult(Response<HolidayProviderStatusDto>.Success(new HolidayProviderStatusDto(_options.Enabled, _options.Provider, host, _options.TimeoutSeconds, _options.MaxResponseItems))); }
    public Task<Response<HolidayAutoFetchScheduleDto>> Handle(GetWorkingCalendarImportScheduleQuery request, CancellationToken ct)
        => Task.FromResult(Response<HolidayAutoFetchScheduleDto>.Success(new HolidayAutoFetchScheduleDto(_options.Schedule.Enabled, _options.Schedule.CronExpression,
            _options.Schedule.YearOffsets, _options.Schedule.MaxTargetsPerRun, _options.Schedule.IncludeNonPublicTypes)));
    private static WorkingCalendarImportBatchDto Map(WorkingCalendarImportBatch x) => new(x.Id, x.BatchCode, x.CountryCode,
        x.CalendarYear, x.TargetCalendarId, x.TargetCalendarCodeSnapshot, x.IncludeNonPublicTypes, x.ImportStatus,
        x.ProviderKey, x.ProviderOutcome, x.CandidateCount, x.ApprovedCount, x.RejectedCount, x.UndecidedCount,
        x.SkippedNonPublicCount, x.DuplicateSourceRowCount, x.TriggerSource, x.RequestedBy, x.RequestedAt, x.AppliedBy,
        x.AppliedAt, x.FailureReason, x.Notes, x.Version, x.Candidates.Select(c => new WorkingCalendarImportCandidateDto(
            c.CandidateId, c.ProviderDayKey, c.Date, c.ProviderName, c.ProviderLocalName, c.ProviderTypes,
            c.MappedDayType, c.MappedDayCode, c.MappedDayName, c.ChangeKind, c.Flags, c.Decision, c.DecisionReason,
            c.DecidedBy, c.DecidedAt, c.AppliedDayId)).ToList());
}
