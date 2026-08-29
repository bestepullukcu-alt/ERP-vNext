using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;

/// <summary>The period picker a consumer UI binds to. It exposes exactly the read seam's shape and no more — a picker
/// is not a back door into audit fields.
/// <para>Its scope arguments FILTER, exactly as the grid's do. They deliberately do not resolve: a picker shows what
/// exists at the addresses asked about, and choosing between them is the human's job. Precedence lives in one place
/// only — <c>resolve-active</c>.</para></summary>
public sealed class GetCyclePeriodSelectorHandler
    : IRequestHandler<GetCyclePeriodSelectorQuery, Response<CyclePeriodSelectorDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICyclePeriodRepository _periods;

    public GetCyclePeriodSelectorHandler(ITenantContext tenant, ICyclePeriodRepository periods)
    {
        _tenant = tenant;
        _periods = periods;
    }

    public async Task<Response<CyclePeriodSelectorDto>> Handle(
        GetCyclePeriodSelectorQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<CyclePeriodSelectorDto>.Fail("Tenant context is required.", 400);
        }

        if (CyclePeriodValidation.ValidateStatusFilter(request.CycleStatus) is { } statusFailure)
        {
            return Response<CyclePeriodSelectorDto>.Fail(
                CyclePeriodValidation.ToErrors(statusFailure), statusFailure.StatusCode);
        }

        if (CyclePeriodValidation.ValidateScopeTypeFilter(request.ScopeType) is { } scopeTypeFailure)
        {
            return Response<CyclePeriodSelectorDto>.Fail(
                CyclePeriodValidation.ToErrors(scopeTypeFailure), scopeTypeFailure.StatusCode);
        }

        var rows = request.Year is { } year
            ? await _periods.ListByYearAsync(tenantId, year, cancellationToken)
            : await _periods.ListAsync(tenantId, cancellationToken);

        IEnumerable<Domain.Entities.CyclePeriod> filtered = rows;

        if (!string.IsNullOrWhiteSpace(request.CycleStatus))
        {
            var status = CyclePeriodStatuses.Normalize(request.CycleStatus);
            filtered = filtered.Where(p => string.Equals(p.CycleStatus, status, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.ScopeType))
        {
            var scopeType = CyclePeriodScopeTypes.Normalize(request.ScopeType);
            filtered = filtered.Where(p =>
                string.Equals(p.EffectiveScopeType(), scopeType, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            var country = CyclePeriodScopeRules.NormalizeCountry(request.Country);
            filtered = filtered.Where(p => CyclePeriodOverlapRules.IsAtScope(
                p, CyclePeriodScopeTypes.Country, country));
        }

        if (request.LegalEntityId is { } legalEntityId && legalEntityId != Guid.Empty)
        {
            filtered = filtered.Where(p => CyclePeriodOverlapRules.IsAtScope(
                p, CyclePeriodScopeTypes.LegalEntity, legalEntityId.ToString("D")));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessUnitId))
        {
            filtered = filtered.Where(p => CyclePeriodOverlapRules.IsAtScope(
                p, CyclePeriodScopeTypes.BusinessUnit, request.BusinessUnitId.Trim()));
        }

        var items = filtered
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.SequenceInYear)
            .Select(CyclePeriodMapper.ToSelectorItem)
            .ToList();

        return Response<CyclePeriodSelectorDto>.Success(new CyclePeriodSelectorDto(items, items.Count));
    }
}
