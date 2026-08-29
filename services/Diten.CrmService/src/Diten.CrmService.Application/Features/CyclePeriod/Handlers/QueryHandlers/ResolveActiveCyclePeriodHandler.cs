using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>
/// The HTTP face of <see cref="ICyclePeriodReader"/>. It is a thin forwarder BY DESIGN: the resolution rule exists in
/// exactly one place, so the endpoint and an in-process consumer (MOD-0155 MicroTarget) can never answer differently.
/// <para>It writes nothing. No period is created, no status is touched and no "current period" is remembered anywhere —
/// asking which period is in force must never have a side effect.</para>
/// <para>The request scope is echoed back alongside <c>ResolvedScopeType</c>, so an answer is self-describing: a
/// consumer can tell "my business unit has its own calendar" from "my business unit is following the tenant's" without
/// a second call.</para>
/// </summary>
public sealed class ResolveActiveCyclePeriodHandler
    : IRequestHandler<ResolveActiveCyclePeriodQuery, Response<CyclePeriodResolutionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodReader _reader;

    public ResolveActiveCyclePeriodHandler(ITenantContext tenant, ICyclePeriodReader reader)
    {
        _tenant = tenant;
        _reader = reader;
    }

    public async Task<Response<CyclePeriodResolutionDto>> Handle(
        ResolveActiveCyclePeriodQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Response<CyclePeriodResolutionDto>.Fail("Tenant context is required.", 400);
        }

        var country = CyclePeriodScopeRules.NormalizeCountry(request.Country);
        var legalEntityId = request.LegalEntityId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var businessUnitId = CyclePeriodScopeRules.Trim(request.BusinessUnitId);

        var resolution = await _reader.ResolveActiveAsync(
            request.At, country, legalEntityId, businessUnitId, cancellationToken);

        var dto = new CyclePeriodResolutionDto(
            resolution.Outcome,
            request.At,
            country,
            legalEntityId,
            businessUnitId,
            resolution.ResolvedScopeType,
            resolution.Period is null ? null : CyclePeriodMapper.ToSelectorItem(resolution.Period),
            resolution.CandidateIds,
            resolution.Reason);

        return Response<CyclePeriodResolutionDto>.Success(dto);
    }
}
