using System.Globalization;
using System.Text.RegularExpressions;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;

/// <summary>
/// WP-VP-3D — <c>GET api/crm/visit-plan/my-accounts/{accountId}/doctors</c>: an institution's ACTIVE doctors (the
/// WP-VP-2B active-link rule, <see cref="RelationshipLifecycle.IsActiveLink"/>) with each one's period status
/// (<see cref="ContactPeriodStatusReader"/>). A READ query — nothing is written or audited.
/// <para><b>Whose period.</b> With <paramref name="PlanningSessionId"/> the plan's rep and CyclePeriod (a plan the caller
/// may not see is 404 — the WP-VP-2 session rule). Without it, the caller (a <c>read-all</c> holder may name
/// <paramref name="ResourceId"/>; anyone else asking for another rep is 403 <c>resource_not_caller</c>) and the
/// tenant-level period in force today.</para>
/// <para><b>Out of territory (K-5).</b> An institution outside the rep's current coverage is still answered, flagged
/// <c>outOfTerritory = true</c> — never hidden. A rep with no current assignment covers the whole tenant.</para>
/// <para><b>Filters.</b> <c>quick</c> = <c>all</c> (default) · <c>due</c> (dueThisWeek) · <c>never</c> (neverVisited);
/// anything else is 400 <c>invalid_quick</c>. <c>search</c> is Turkish-insensitive on the name
/// (<see cref="TurkishInsensitivePattern"/>); <c>specialty</c> is an exact (case-insensitive) code. At most
/// <see cref="MaxDoctorsPerAccount"/> doctors per institution are evaluated (by name); the response is paged.</para>
/// </summary>
public sealed record GetAccountDoctorsQuery(
    Guid AccountId,
    Guid? PlanningSessionId = null,
    string? Quick = null,
    string? Search = null,
    string? Specialty = null,
    int Page = 1,
    int PageSize = 50,
    string? ResourceId = null,
    // WP-VP-4L (2) — with a plan: the week (Monday yyyy-MM-dd) whose extra visits extraThisWeek reads.
    string? WeekStart = null) : IRequest<Response<AccountDoctorsDto>>;

public sealed class GetAccountDoctorsQueryHandler : IRequestHandler<GetAccountDoctorsQuery, Response<AccountDoctorsDto>>
{
    /// <summary>The per-institution ceiling of evaluated doctors (a hospital's roster, not the tenant).</summary>
    public const int MaxDoctorsPerAccount = 500;

    private const int MaxPageSize = 200;
    private static readonly StringComparer NameOrder = StringComparer.Create(new CultureInfo("tr-TR"), true);

    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlanningSessionRepository _sessions;
    private readonly ICyclePeriodReader _periods;
    private readonly IAccountRepository _accounts;
    private readonly IAccountContactLinkRepository _links;
    private readonly IContactRepository _contacts;
    private readonly ITerritoryResourceAssignmentRepository _resourceAssignments;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryModelRepository _models;
    private readonly IAccountTerritoryAssignmentRepository _accountAssignments;
    private readonly ContactPeriodStatusReader _status;

    public GetAccountDoctorsQueryHandler(
        ITenantContext tenant,
        ICallerScope caller,
        IPlanningSessionRepository sessions,
        ICyclePeriodReader periods,
        IAccountRepository accounts,
        IAccountContactLinkRepository links,
        IContactRepository contacts,
        ITerritoryResourceAssignmentRepository resourceAssignments,
        ITerritoryNodeRepository nodes,
        ITerritoryModelRepository models,
        IAccountTerritoryAssignmentRepository accountAssignments,
        ContactPeriodStatusReader status)
    {
        _tenant = tenant;
        _caller = caller;
        _sessions = sessions;
        _periods = periods;
        _accounts = accounts;
        _links = links;
        _contacts = contacts;
        _resourceAssignments = resourceAssignments;
        _nodes = nodes;
        _models = models;
        _accountAssignments = accountAssignments;
        _status = status;
    }

    public async Task<Response<AccountDoctorsDto>> Handle(GetAccountDoctorsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AccountDoctorsDto>.Fail("Tenant context is required.", 400);
        }

        if (!TargetStatusQuickFilters.TryNormalize(request.Quick, out var quick))
        {
            return Response<AccountDoctorsDto>.Fail(
                new[] { TargetStatusQuickFilters.InvalidQuickCode, "quick must be 'due', 'never' or 'all'." }, 400);
        }

        var now = DateTimeOffset.UtcNow;
        string resourceId;
        ContactStatusPeriod? period;
        PlanningSession? planSession = null;
        if (request.PlanningSessionId is { } sessionId && sessionId != Guid.Empty)
        {
            var session = await _sessions.GetByIdAsync(tenantId, sessionId, cancellationToken);
            if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
            {
                return Response<AccountDoctorsDto>.Fail("Planning session not found.", 404);
            }

            resourceId = session.ResourceId.Trim();
            period = TargetStatusPeriods.From(await _periods.GetByIdAsync(session.CyclePeriodId, cancellationToken));
            planSession = session;
        }
        else
        {
            var (allowed, resolved) = _caller.ResolveWriteResource(VisitPlanningPermissions.ReadAll, request.ResourceId);
            if (!allowed || resolved is null)
            {
                return Response<AccountDoctorsDto>.Fail(
                    new[] { VisitOwnership.ResourceNotCaller, "Only your own doctors can be listed." }, 403);
            }

            resourceId = resolved;
            period = await TargetStatusPeriods.ActiveAsync(_periods, now, cancellationToken);
        }

        // WP-VP-4M (1) — "this week" is the selected week when one is asked (a Monday of the period).
        if (!TargetStatusPeriods.TryParseSelectedWeek(request.WeekStart, period, out var selectedWeek))
        {
            return Response<AccountDoctorsDto>.Fail(new[]
            {
                Handlers.CommandHandlers.PlanningSessionErrorCodes.InvalidWeek,
                "weekStart must be a Monday (yyyy-MM-dd) of a week of the period."
            }, 400);
        }

        var account = await _accounts.GetByIdAsync(tenantId, request.AccountId, cancellationToken);
        if (account is null)
        {
            return Response<AccountDoctorsDto>.Fail("Account not found.", 404);
        }

        var outOfTerritory = await IsOutOfTerritoryAsync(tenantId, resourceId, account.Id, now, cancellationToken);

        // The account's ACTIVE doctors: links by account (1 read) + their contacts (1 read), the WP-VP-2B rule.
        var links = (await _links.ListByAccountAsync(tenantId, account.Id, cancellationToken))
            .Where(l => l.TenantId == tenantId)
            .ToList();
        var contacts = links.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Contact>()
            : (await _contacts.ListByIdsAsync(tenantId, links.Select(l => l.ContactId).Distinct().ToList(), cancellationToken))
                .Where(c => c.TenantId == tenantId && !c.IsDeleted)
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First());

        var regex = string.IsNullOrWhiteSpace(request.Search)
            ? null
            : new Regex(TurkishInsensitivePattern.Build(request.Search.Trim()),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        var specialty = string.IsNullOrWhiteSpace(request.Specialty) ? null : request.Specialty.Trim();

        var doctors = links
            .Where(l => RelationshipLifecycle.IsActiveLink(l, contacts.ContainsKey(l.ContactId)))
            .GroupBy(l => l.ContactId)
            .Select(g => (Link: g.OrderByDescending(l => l.IsPrimary).ThenBy(l => l.Id).First(), Contact: contacts[g.Key]))
            .Where(d => specialty is null
                        || string.Equals(d.Contact.Specialty?.Trim(), specialty, StringComparison.OrdinalIgnoreCase))
            .Where(d => regex is null || regex.IsMatch(d.Contact.DisplayName ?? string.Empty))
            .OrderBy(d => d.Contact.DisplayName, NameOrder)
            .ThenBy(d => d.Contact.Id)
            .Take(MaxDoctorsPerAccount)
            .ToList();

        var statuses = await _status.ReadAsync(
            new ContactPeriodStatusRequest(
                tenantId, resourceId, period, doctors.Select(d => d.Contact.Id).ToList(),
                DateOnly.FromDateTime(now.UtcDateTime), now, doctors.Select(d => d.Contact).ToList(), selectedWeek),
            cancellationToken);

        var withStatus = doctors
            .Select(d => (d.Link, d.Contact, Status: TargetStatusPeriods.WithExtraWeek(statuses[d.Contact.Id], planSession, request.WeekStart)))
            .ToList();
        // WP-VP-4M (1) — the quick filters' counts from the SAME statuses the filter below reads.
        var quickCounts = new TargetQuickCountsDto(
            withStatus.Count(d => d.Status.DueThisWeek), withStatus.Count(d => d.Status.NeverVisited), withStatus.Count);
        var filtered = withStatus
            .Where(d => quick switch
            {
                TargetStatusQuickFilters.Due => d.Status.DueThisWeek,
                TargetStatusQuickFilters.Never => d.Status.NeverVisited,
                _ => true
            })
            .ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new AccountDoctorItemDto(
                d.Contact.Id, d.Link.Id, d.Contact.DisplayName, d.Contact.Specialty, d.Contact.ProfessionalTitle,
                d.Link.IsPrimary, d.Status))
            .ToList();

        return Response<AccountDoctorsDto>.Success(new AccountDoctorsDto(
            account.Id, account.AccountName, outOfTerritory, TargetStatusPeriods.ToDto(period),
            items, filtered.Count, page, pageSize)
        {
            QuickCounts = quickCounts
        });
    }

    /// <summary>K-5 — is the account outside the rep's CURRENT coverage? The "my accounts" rule
    /// (<see cref="RepTerritoryCoverage"/>); no current assignment ⇒ the whole tenant ⇒ never out.</summary>
    private async Task<bool> IsOutOfTerritoryAsync(
        Guid tenantId, string resourceId, Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await RepTerritoryCoverage.CurrentAssignmentsAsync(
            _resourceAssignments, _models, tenantId, resourceId, now, cancellationToken);
        if (current.Count == 0)
        {
            return false;
        }

        var nodes = await RepTerritoryCoverage.CoveredNodesAsync(_nodes, tenantId, current, now, cancellationToken);
        var covered = await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsByNodesAsync(
            _accountAssignments, _models, tenantId, nodes, now, cancellationToken);
        return !covered.Contains(accountId);
    }
}
