using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContactAvailability.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAvailability = Diten.CrmService.Domain.Entities.ContactAvailability;
using DomainException = Diten.CrmService.Domain.Entities.ContactAvailabilityException;

namespace Diten.CrmService.Application.Features.ContactAvailability.Handlers;

/// <summary>Shared read plumbing: link → availability/exception grouping and display-name enrichment.</summary>
internal sealed class ContactAvailabilityReader
{
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactAvailabilityRepository _availability;
    private readonly IContactAvailabilityExceptionRepository _exceptions;
    private readonly IContactRepository _contacts;
    private readonly IAccountRepository _accounts;

    public ContactAvailabilityReader(
        IAccountContactLinkRepository links,
        IContactAvailabilityRepository availability,
        IContactAvailabilityExceptionRepository exceptions,
        IContactRepository contacts,
        IAccountRepository accounts)
    {
        _links = links;
        _availability = availability;
        _exceptions = exceptions;
        _contacts = contacts;
        _accounts = accounts;
    }

    public async Task<IReadOnlyList<LinkAvailabilityDto>> BuildAsync(
        Guid tenantId, IReadOnlyList<AccountContactLink> links, CancellationToken cancellationToken)
    {
        if (links.Count == 0)
        {
            return [];
        }

        var linkIds = links.Select(l => l.Id).Distinct().ToList();
        var availability = await _availability.ListByLinkIdsAsync(tenantId, linkIds, cancellationToken);
        var exceptions = await _exceptions.ListByLinkIdsAsync(tenantId, linkIds, cancellationToken);

        var contactNames = new Dictionary<Guid, string?>();
        var accountNames = new Dictionary<Guid, (string? Name, string? Code)>();

        foreach (var link in links)
        {
            if (!contactNames.ContainsKey(link.ContactId))
            {
                var contact = await _contacts.GetByIdAsync(tenantId, link.ContactId, cancellationToken);
                contactNames[link.ContactId] = contact?.DisplayName;
            }

            if (!accountNames.ContainsKey(link.AccountId))
            {
                var account = await _accounts.GetByIdAsync(tenantId, link.AccountId, cancellationToken);
                accountNames[link.AccountId] = (account?.AccountName, account?.AccountCode);
            }
        }

        return links
            .Select(link =>
            {
                var contactName = contactNames.GetValueOrDefault(link.ContactId);
                var (accountName, accountCode) = accountNames.GetValueOrDefault(link.AccountId);

                return new LinkAvailabilityDto(
                    link.Id,
                    link.ContactId,
                    contactName,
                    link.AccountId,
                    accountName,
                    accountCode,
                    link.RoleCode,
                    link.IsPrimary,
                    ContactAvailabilityValidation.IsLinkOpen(link),
                    availability
                        .Where(a => a.AccountContactLinkId == link.Id)
                        .OrderBy(a => WeekdayOrder(a.Weekday))
                        .ThenBy(a => a.StartTime, StringComparer.Ordinal)
                        .Select(a => ContactAvailabilityMapper.ToDto(a, contactName, accountName, accountCode))
                        .ToList(),
                    exceptions
                        .Where(e => e.AccountContactLinkId == link.Id)
                        .OrderBy(e => e.Date, StringComparer.Ordinal)
                        .Select(e => ContactAvailabilityMapper.ToDto(e, contactName, accountName))
                        .ToList());
            })
            .ToList();
    }

    /// <summary>Monday-first ordering for grids; an unknown value sorts last instead of throwing.</summary>
    private static int WeekdayOrder(string weekday)
    {
        for (var index = 0; index < AvailabilityWeekday.All.Count; index++)
        {
            if (string.Equals(AvailabilityWeekday.All[index], weekday, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return AvailabilityWeekday.All.Count;
    }

    public Task<IReadOnlyList<AccountContactLink>> LinksByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct)
        => _links.ListByContactAsync(tenantId, contactId, ct);

    public Task<IReadOnlyList<AccountContactLink>> LinksByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        => _links.ListByAccountAsync(tenantId, accountId, ct);

    public Task<AccountContactLink?> LinkAsync(Guid tenantId, Guid linkId, CancellationToken ct)
        => _links.GetByIdAsync(tenantId, linkId, ct);
}

public sealed class ListContactAvailabilityHandler
    : IRequestHandler<ListContactAvailabilityQuery, Response<IReadOnlyList<LinkAvailabilityDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly ContactAvailabilityReader _reader;

    public ListContactAvailabilityHandler(
        ITenantContext tenant, IContactRepository contacts, IAccountContactLinkRepository links,
        IContactAvailabilityRepository availability, IContactAvailabilityExceptionRepository exceptions, IAccountRepository accounts)
    {
        _tenant = tenant;
        _contacts = contacts;
        _reader = new ContactAvailabilityReader(links, availability, exceptions, contacts, accounts);
    }

    public async Task<Response<IReadOnlyList<LinkAvailabilityDto>>> Handle(
        ListContactAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<LinkAvailabilityDto>>.Fail("Tenant context is required.", 400);
        }

        // Cross-tenant / missing / soft-deleted contact is a 404 — never an empty 200 that looks like "no data".
        var contact = await _contacts.GetByIdAsync(tenantId, request.ContactId, cancellationToken);
        if (contact is null)
        {
            return Response<IReadOnlyList<LinkAvailabilityDto>>.Fail("Contact not found.", 404);
        }

        var links = await _reader.LinksByContactAsync(tenantId, request.ContactId, cancellationToken);
        return Response<IReadOnlyList<LinkAvailabilityDto>>.Success(
            await _reader.BuildAsync(tenantId, links, cancellationToken));
    }
}

public sealed class GetLinkAvailabilityHandler : IRequestHandler<GetLinkAvailabilityQuery, Response<LinkAvailabilityDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ContactAvailabilityReader _reader;

    public GetLinkAvailabilityHandler(
        ITenantContext tenant, IAccountContactLinkRepository links, IContactAvailabilityRepository availability,
        IContactAvailabilityExceptionRepository exceptions, IContactRepository contacts, IAccountRepository accounts)
    {
        _tenant = tenant;
        _reader = new ContactAvailabilityReader(links, availability, exceptions, contacts, accounts);
    }

    public async Task<Response<LinkAvailabilityDto>> Handle(GetLinkAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<LinkAvailabilityDto>.Fail("Tenant context is required.", 400);
        }

        var link = await _reader.LinkAsync(tenantId, request.AccountContactLinkId, cancellationToken);
        if (link is null)
        {
            return Response<LinkAvailabilityDto>.Fail("Account-contact link not found.", 404);
        }

        var built = await _reader.BuildAsync(tenantId, [link], cancellationToken);
        return Response<LinkAvailabilityDto>.Success(built[0]);
    }
}

public sealed class ListAccountContactAvailabilityHandler
    : IRequestHandler<ListAccountContactAvailabilityQuery, Response<IReadOnlyList<LinkAvailabilityDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountRepository _accounts;
    private readonly ContactAvailabilityReader _reader;

    public ListAccountContactAvailabilityHandler(
        ITenantContext tenant, IAccountRepository accounts, IAccountContactLinkRepository links,
        IContactAvailabilityRepository availability, IContactAvailabilityExceptionRepository exceptions, IContactRepository contacts)
    {
        _tenant = tenant;
        _accounts = accounts;
        _reader = new ContactAvailabilityReader(links, availability, exceptions, contacts, accounts);
    }

    public async Task<Response<IReadOnlyList<LinkAvailabilityDto>>> Handle(
        ListAccountContactAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<LinkAvailabilityDto>>.Fail("Tenant context is required.", 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
        {
            return Response<IReadOnlyList<LinkAvailabilityDto>>.Fail("Account not found.", 404);
        }

        var links = await _reader.LinksByAccountAsync(tenantId, request.AccountId, cancellationToken);
        return Response<IReadOnlyList<LinkAvailabilityDto>>.Success(
            await _reader.BuildAsync(tenantId, links, cancellationToken));
    }
}

/// <summary>
/// MOD-0150 FU07 lookup — the readiness seam consumed by MOD-0151 FU09A and (later) MOD-0155.
/// <para>
/// It answers "when can this person be visited at this location on this date", applying date-specific exceptions over
/// the weekly pattern. It deliberately does NOT decide anything: no ordering, no distance, no score, no plan. Missing
/// data returns <c>unknown</c> with <c>no_availability_data</c> — never <c>unavailable</c> (pack D15 / MOD-0151 R11).
/// </para>
/// </summary>
public sealed class LookupContactAvailabilityHandler
    : IRequestHandler<LookupContactAvailabilityQuery, Response<ContactAvailabilityLookupDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactAvailabilityRepository _availability;
    private readonly IContactAvailabilityExceptionRepository _exceptions;
    private readonly IContactRepository _contacts;
    private readonly IAccountRepository _accounts;

    public LookupContactAvailabilityHandler(
        ITenantContext tenant, IAccountContactLinkRepository links, IContactAvailabilityRepository availability,
        IContactAvailabilityExceptionRepository exceptions, IContactRepository contacts, IAccountRepository accounts)
    {
        _tenant = tenant;
        _links = links;
        _availability = availability;
        _exceptions = exceptions;
        _contacts = contacts;
        _accounts = accounts;
    }

    public async Task<Response<ContactAvailabilityLookupDto>> Handle(
        LookupContactAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContactAvailabilityLookupDto>.Fail("Tenant context is required.", 400);
        }

        if (ContactAvailabilityValidation.ParseDate(request.Date) is not { } date)
        {
            return Response<ContactAvailabilityLookupDto>.Fail("date must be a calendar date in yyyy-MM-dd format.", 400);
        }

        if (request.AccountContactLinkId is null && request.ContactId is null && request.AccountId is null)
        {
            return Response<ContactAvailabilityLookupDto>.Fail(
                "At least one of accountContactLinkId, contactId or accountId is required.", 400);
        }

        var links = await ResolveLinksAsync(tenantId, request, cancellationToken);
        var weekday = AvailabilityWeekday.FromDate(date);

        if (links.Count == 0)
        {
            // No link at all is still not "unavailable": the caller learns there is nothing to say.
            return Response<ContactAvailabilityLookupDto>.Success(
                new ContactAvailabilityLookupDto(date.ToString("yyyy-MM-dd"), weekday, []));
        }

        var linkIds = links.Select(l => l.Id).ToList();
        var availabilityRows = await _availability.ListByLinkIdsAsync(tenantId, linkIds, cancellationToken);
        var exceptionRows = await _exceptions.ListByLinkIdsAsync(tenantId, linkIds, cancellationToken);
        var normalizedDate = date.ToString("yyyy-MM-dd");

        var rows = new List<ContactAvailabilityLookupRowDto>();
        foreach (var link in links)
        {
            var contact = await _contacts.GetByIdAsync(tenantId, link.ContactId, cancellationToken);
            var account = await _accounts.GetByIdAsync(tenantId, link.AccountId, cancellationToken);
            rows.AddRange(BuildRows(
                link,
                contact?.DisplayName,
                account?.AccountName,
                availabilityRows.Where(a => a.AccountContactLinkId == link.Id).ToList(),
                exceptionRows.Where(e => e.AccountContactLinkId == link.Id).ToList(),
                date,
                normalizedDate,
                weekday));
        }

        return Response<ContactAvailabilityLookupDto>.Success(
            new ContactAvailabilityLookupDto(normalizedDate, weekday, rows));
    }

    private async Task<IReadOnlyList<AccountContactLink>> ResolveLinksAsync(
        Guid tenantId, LookupContactAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (request.AccountContactLinkId is { } linkId)
        {
            var link = await _links.GetByIdAsync(tenantId, linkId, cancellationToken);
            return link is null ? [] : [link];
        }

        IEnumerable<AccountContactLink> links = request.ContactId is { } contactId
            ? await _links.ListByContactAsync(tenantId, contactId, cancellationToken)
            : await _links.ListByAccountAsync(tenantId, request.AccountId!.Value, cancellationToken);

        // Both filters together narrow to the single contact-at-account link(s).
        if (request.ContactId is { } c && request.AccountId is { } a)
        {
            links = links.Where(l => l.ContactId == c && l.AccountId == a);
        }

        return links.ToList();
    }

    /// <summary>
    /// One link → zero or more lookup rows. A date-specific exception wins over the weekly pattern (pack D12); when
    /// no active availability exists at all the row is <c>unknown</c>, never <c>unavailable</c>.
    /// </summary>
    private static IEnumerable<ContactAvailabilityLookupRowDto> BuildRows(
        AccountContactLink link,
        string? contactName,
        string? accountName,
        IReadOnlyList<DomainAvailability> availability,
        IReadOnlyList<DomainException> exceptions,
        DateOnly date,
        string normalizedDate,
        string weekday)
    {
        var linkClosed = !ContactAvailabilityValidation.IsLinkOpen(link);
        var activeRows = availability.Where(a => !AvailabilityLifecycle.IsClosed(a.Status)).ToList();
        var dayRows = activeRows
            .Where(a => string.Equals(a.Weekday, weekday, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var effectiveDayRows = dayRows
            .Where(a => ContactAvailabilityValidation.IsEffectiveOn(a.EffectiveFrom, a.EffectiveTo, date))
            .OrderBy(a => a.StartTime, StringComparer.Ordinal)
            .ToList();

        var exception = exceptions.FirstOrDefault(e =>
            !AvailabilityLifecycle.IsClosed(e.Status) && string.Equals(e.Date, normalizedDate, StringComparison.Ordinal));

        // 1) Date-specific exception: strictly stronger than the weekly pattern.
        if (exception is not null)
        {
            var reasons = new List<string>();
            var template = effectiveDayRows.FirstOrDefault();

            if (!exception.IsAvailable)
            {
                reasons.Add(AvailabilityReasonCodes.ExceptionUnavailable);
                if (linkClosed) reasons.Add(AvailabilityReasonCodes.LinkInactive);

                yield return new ContactAvailabilityLookupRowDto(
                    link.Id, link.ContactId, contactName, link.AccountId, accountName, weekday,
                    AvailableWindow: null, PreferredWindow: null, AvoidWindow: null,
                    AppointmentRequired: template?.Preference.AppointmentRequired ?? false,
                    AppointmentLeadTimeDays: template?.Preference.AppointmentLeadTimeDays,
                    AverageVisitDurationMinutes: template?.AverageVisitDurationMinutes,
                    AvailabilityStatus: AvailabilityLookupStatus.Unavailable,
                    ExceptionApplied: true,
                    ExceptionReason: exception.Reason,
                    ReasonCodes: reasons);
                yield break;
            }

            reasons.Add(AvailabilityReasonCodes.ExceptionWindowApplied);
            var window = ContactAvailabilityMapper.Window(exception.StartTime, exception.EndTime)
                         ?? ContactAvailabilityMapper.Window(template?.StartTime, template?.EndTime);
            AddPreferenceReasons(reasons, template);
            if (linkClosed) reasons.Add(AvailabilityReasonCodes.LinkInactive);

            yield return new ContactAvailabilityLookupRowDto(
                link.Id, link.ContactId, contactName, link.AccountId, accountName, weekday,
                window,
                ContactAvailabilityMapper.Window(template?.Preference.PreferredVisitStartTime, template?.Preference.PreferredVisitEndTime),
                ContactAvailabilityMapper.Window(template?.Preference.AvoidVisitStartTime, template?.Preference.AvoidVisitEndTime),
                template?.Preference.AppointmentRequired ?? false,
                template?.Preference.AppointmentLeadTimeDays,
                template?.AverageVisitDurationMinutes,
                linkClosed ? AvailabilityLookupStatus.Unavailable : AvailabilityLookupStatus.Available,
                ExceptionApplied: true,
                ExceptionReason: exception.Reason,
                ReasonCodes: reasons);
            yield break;
        }

        // 2) No active availability anywhere on this link → UNKNOWN (data absence, not a rule violation).
        if (activeRows.Count == 0)
        {
            var reasons = new List<string> { AvailabilityReasonCodes.NoAvailabilityData };
            if (availability.Count > 0) reasons.Add(AvailabilityReasonCodes.AvailabilityInactive);
            if (linkClosed) reasons.Add(AvailabilityReasonCodes.LinkInactive);

            yield return new ContactAvailabilityLookupRowDto(
                link.Id, link.ContactId, contactName, link.AccountId, accountName, weekday,
                null, null, null, false, null, null,
                AvailabilityLookupStatus.Unknown, ExceptionApplied: false, ExceptionReason: null, ReasonCodes: reasons);
            yield break;
        }

        // 3) Another weekday's data says nothing about this weekday: absence remains UNKNOWN.
        if (effectiveDayRows.Count == 0)
        {
            var hasRowsForWeekday = dayRows.Count > 0;
            var reasons = new List<string>
            {
                hasRowsForWeekday
                    ? AvailabilityReasonCodes.OutsideEffectiveWindow
                    : AvailabilityReasonCodes.NoAvailabilityData
            };
            if (linkClosed) reasons.Add(AvailabilityReasonCodes.LinkInactive);

            yield return new ContactAvailabilityLookupRowDto(
                link.Id, link.ContactId, contactName, link.AccountId, accountName, weekday,
                null, null, null, false, null, null,
                hasRowsForWeekday ? AvailabilityLookupStatus.Unavailable : AvailabilityLookupStatus.Unknown,
                ExceptionApplied: false, ExceptionReason: null, ReasonCodes: reasons);
            yield break;
        }

        // 4) One row per effective window that day.
        foreach (var row in effectiveDayRows)
        {
            var reasons = new List<string> { AvailabilityReasonCodes.AvailabilityOk };
            AddPreferenceReasons(reasons, row);
            if (linkClosed) reasons.Add(AvailabilityReasonCodes.LinkInactive);

            yield return new ContactAvailabilityLookupRowDto(
                link.Id, link.ContactId, contactName, link.AccountId, accountName, weekday,
                ContactAvailabilityMapper.Window(row.StartTime, row.EndTime),
                ContactAvailabilityMapper.Window(row.Preference.PreferredVisitStartTime, row.Preference.PreferredVisitEndTime),
                ContactAvailabilityMapper.Window(row.Preference.AvoidVisitStartTime, row.Preference.AvoidVisitEndTime),
                row.Preference.AppointmentRequired,
                row.Preference.AppointmentLeadTimeDays,
                row.AverageVisitDurationMinutes,
                linkClosed ? AvailabilityLookupStatus.Unavailable : AvailabilityLookupStatus.Available,
                ExceptionApplied: false,
                ExceptionReason: null,
                ReasonCodes: reasons);
        }
    }

    /// <summary>Appointment/avoid/preferred are WARNINGS, not filters: they never change the status (pack D14/D13).</summary>
    private static void AddPreferenceReasons(List<string> reasons, DomainAvailability? row)
    {
        if (row is null)
        {
            return;
        }

        if (row.Preference.AppointmentRequired)
        {
            reasons.Add(AvailabilityReasonCodes.AppointmentRequired);
        }

        if (row.Preference.HasAvoidWindow)
        {
            reasons.Add(AvailabilityReasonCodes.AvoidWindowDefined);
        }

        if (row.Preference.HasPreferredWindow)
        {
            reasons.Add(AvailabilityReasonCodes.PreferredWindowDefined);
        }
    }
}
