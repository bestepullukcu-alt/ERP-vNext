using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;

namespace Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;

/// <summary>
/// WP-VP-3D (D5) — <c>GET api/crm/visit-plan/sessions/{id}/targets</c>: everything the plan selected — institutions,
/// pharmacies, doctors — with names, types, city / district / address and coordinates, and each doctor's period status,
/// in ONE response (today the Details page asks <c>/accounts/{id}</c> once per target). Ownership is the session read's
/// own rule: a plan the caller may not see is 404. A READ query — nothing is written or audited.
/// <para>Cost, whatever the plan size: session (1) + period (1) + accounts by id (1, institutions ∪ pharmacies ∪ the
/// doctors' institutions) + contacts by id (1) + <see cref="ContactPeriodStatusReader"/> (bulk).</para>
/// </summary>
public sealed record GetSessionTargetsQuery(Guid PlanningSessionId) : IRequest<Response<SessionTargetsDto>>;

public sealed class GetSessionTargetsQueryHandler : IRequestHandler<GetSessionTargetsQuery, Response<SessionTargetsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly IPlanningSessionRepository _sessions;
    private readonly ICyclePeriodReader _periods;
    private readonly IAccountRepository _accounts;
    private readonly IContactRepository _contacts;
    private readonly ContactPeriodStatusReader _status;
    private readonly IProductNameReader? _productNames;

    public GetSessionTargetsQueryHandler(
        ITenantContext tenant,
        ICallerScope caller,
        IPlanningSessionRepository sessions,
        ICyclePeriodReader periods,
        IAccountRepository accounts,
        IContactRepository contacts,
        ContactPeriodStatusReader status,
        // WP-VP-4G (F4-4) — the picked products' names (one bulk MDM read, fail-open).
        IProductNameReader? productNames = null)
    {
        _productNames = productNames;
        _tenant = tenant;
        _caller = caller;
        _sessions = sessions;
        _periods = periods;
        _accounts = accounts;
        _contacts = contacts;
        _status = status;
    }

    public async Task<Response<SessionTargetsDto>> Handle(GetSessionTargetsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SessionTargetsDto>.Fail("Tenant context is required.", 400);
        }

        var session = await _sessions.GetByIdAsync(tenantId, request.PlanningSessionId, cancellationToken);
        if (session is null || !_caller.MayAccess(VisitPlanningPermissions.ReadAll, session.ResourceId))
        {
            return Response<SessionTargetsDto>.Fail("Planning session not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        var period = TargetStatusPeriods.From(await _periods.GetByIdAsync(session.CyclePeriodId, cancellationToken));
        var selection = session.Selection;

        var accountIds = selection.SelectedAccountIds
            .Concat(selection.SelectedPharmacyIds)
            .Concat(selection.SelectedContacts.Select(c => c.AccountId ?? Guid.Empty))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var accounts = accountIds.Count == 0
            ? new Dictionary<Guid, AccountEntity>()
            : (await _accounts.ListByIdsAsync(tenantId, accountIds, cancellationToken))
                .Where(a => a.TenantId == tenantId && !a.IsDeleted)
                .GroupBy(a => a.Id)
                .ToDictionary(g => g.Key, g => g.First());

        var contactIds = selection.SelectedContacts.Select(c => c.ContactId).Where(id => id != Guid.Empty).Distinct().ToList();
        var contacts = contactIds.Count == 0
            ? new List<Domain.Entities.Contact>()
            : (await _contacts.ListByIdsAsync(tenantId, contactIds, cancellationToken))
                .Where(c => c.TenantId == tenantId && !c.IsDeleted)
                .ToList();
        var contactById = contacts.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());

        var pickedIds = selection.SelectedContacts.SelectMany(c => c.Products).Select(p => p.ProductId).Distinct().ToList();
        var productNames = _productNames is null || pickedIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _productNames.ReadNamesAsync(pickedIds, cancellationToken);

        var statuses = await _status.ReadAsync(
            new ContactPeriodStatusRequest(
                tenantId, session.ResourceId, period, contactIds,
                DateOnly.FromDateTime(now.UtcDateTime), now, contacts),
            cancellationToken);

        SessionTargetAccountDto ToTarget(Guid id) => accounts.TryGetValue(id, out var a)
            ? new SessionTargetAccountDto(
                id, true, a.AccountName, a.AccountCode, a.AccountType, a.CityRef, a.DistrictRef, a.AddressLine,
                a.Latitude, a.Longitude, VisitTargetNameReader.IsInactiveStatus(a.Status))
            : new SessionTargetAccountDto(id, false, null, null, null, null, null, null, null, null, false);

        var doctors = selection.SelectedContacts
            .Where(c => c.ContactId != Guid.Empty)
            .GroupBy(c => (c.ContactId, c.AccountId))
            .Select(g => g.First())
            .Select(c => contactById.TryGetValue(c.ContactId, out var contact)
                ? new SessionTargetDoctorDto(
                    c.ContactId, c.AccountId, c.AccountContactLinkId, true, contact.DisplayName, contact.Specialty,
                    statuses[c.ContactId], PlanningSessionMapper.ProductsOf(c, productNames))
                : new SessionTargetDoctorDto(
                    c.ContactId, c.AccountId, c.AccountContactLinkId, false, null, null, statuses[c.ContactId],
                    PlanningSessionMapper.ProductsOf(c, productNames)))
            .ToList();

        return Response<SessionTargetsDto>.Success(new SessionTargetsDto(
            session.Id,
            session.ResourceId,
            TargetStatusPeriods.ToDto(period),
            selection.SelectedAccountIds.Distinct().Select(ToTarget).ToList(),
            selection.SelectedPharmacyIds.Distinct().Select(ToTarget).ToList(),
            doctors));
    }
}
