using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>
/// WP-CYC-UI-1 — <c>GET /api/crm/cycle-periods/{id}/usage</c>: the records bound to one period, for the details page
/// and the capacity screen's supply / demand chart (WP-CYC-UI-2).
/// <para><b>Tenant-bound twice.</b> The period is loaded in the caller's tenant first (another tenant's id answers 404,
/// never confirming the row exists), and the usage reader is queried with the same tenant id.</para>
/// <para><b>No personal data.</b> No target, no contact, no rep snapshot: the only name in the answer is a session
/// owner's display name, resolved through <see cref="IUserDisplayNameResolver"/> (unknown ids stay null).</para>
/// </summary>
public sealed class GetCyclePeriodUsageHandler : IRequestHandler<GetCyclePeriodUsageQuery, Response<CyclePeriodUsageDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodRepository _periods;
    private readonly ICyclePeriodUsageReader _usage;
    private readonly IUserDisplayNameResolver _names;

    public GetCyclePeriodUsageHandler(
        ITenantContext tenant,
        ICyclePeriodRepository periods,
        ICyclePeriodUsageReader usage,
        IUserDisplayNameResolver names)
    {
        _tenant = tenant;
        _periods = periods;
        _usage = usage;
        _names = names;
    }

    public async Task<Response<CyclePeriodUsageDto>> Handle(
        GetCyclePeriodUsageQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CyclePeriodUsageDto>.Fail("Tenant context is required.", 400);
        }

        var period = await _periods.GetByIdAsync(tenantId, request.CyclePeriodId, cancellationToken);
        if (period is null)
        {
            return Response<CyclePeriodUsageDto>.Fail("Cycle period not found.", 404);
        }

        var snapshot = await _usage.ReadAsync(tenantId, new[] { period.Id }, cancellationToken);

        var ownerIds = snapshot.Sessions
            .Where(s => s.CyclePeriodId == period.Id)
            .Select(s => Guid.TryParse(s.ResourceId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var names = await _names.ResolveAsync(ownerIds, cancellationToken);

        return Response<CyclePeriodUsageDto>.Success(CyclePeriodUsageRules.Build(period.Id, snapshot, names));
    }
}
