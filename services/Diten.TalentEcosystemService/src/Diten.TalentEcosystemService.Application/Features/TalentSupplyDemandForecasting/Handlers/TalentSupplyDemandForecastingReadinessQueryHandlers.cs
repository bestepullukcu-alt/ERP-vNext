using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentSupplyDemandForecasting.Handlers;

public sealed class GetTalentSupplyDemandForecastingReadinessListHandler
    : IRequestHandler<GetTalentSupplyDemandForecastingReadinessListQuery, Response<IReadOnlyList<TalentSupplyDemandForecastingReadinessListItemDto>>>
{
    private readonly ITalentSupplyDemandForecastingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTalentSupplyDemandForecastingReadinessListHandler(ITalentSupplyDemandForecastingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<TalentSupplyDemandForecastingReadinessListItemDto>>> Handle(
        GetTalentSupplyDemandForecastingReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = TalentSupplyDemandForecastingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<TalentSupplyDemandForecastingReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<TalentSupplyDemandForecastingReadinessListItemDto>>.Success(rows.Select(TalentSupplyDemandForecastingMapper.ToListItem).ToList());
    }
}

public sealed class GetTalentSupplyDemandForecastingReadinessByIdHandler
    : IRequestHandler<GetTalentSupplyDemandForecastingReadinessByIdQuery, Response<TalentSupplyDemandForecastingReadinessDto>>
{
    private readonly ITalentSupplyDemandForecastingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTalentSupplyDemandForecastingReadinessByIdHandler(ITalentSupplyDemandForecastingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TalentSupplyDemandForecastingReadinessDto>> Handle(GetTalentSupplyDemandForecastingReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = TalentSupplyDemandForecastingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentSupplyDemandForecastingReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<TalentSupplyDemandForecastingReadinessDto>.Fail("TalentSupplyDemandForecasting readiness record was not found.", 404)
            : Response<TalentSupplyDemandForecastingReadinessDto>.Success(TalentSupplyDemandForecastingMapper.ToDto(entity));
    }
}

public sealed class GetTalentSupplyDemandForecastingAuditMetadataHandler
    : IRequestHandler<GetTalentSupplyDemandForecastingAuditMetadataQuery, Response<TalentSupplyDemandForecastingAuditMetadataDto>>
{
    private readonly ITalentSupplyDemandForecastingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTalentSupplyDemandForecastingAuditMetadataHandler(ITalentSupplyDemandForecastingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TalentSupplyDemandForecastingAuditMetadataDto>> Handle(GetTalentSupplyDemandForecastingAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = TalentSupplyDemandForecastingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TalentSupplyDemandForecastingAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<TalentSupplyDemandForecastingAuditMetadataDto>.Fail("TalentSupplyDemandForecasting readiness record was not found.", 404)
            : Response<TalentSupplyDemandForecastingAuditMetadataDto>.Success(TalentSupplyDemandForecastingMapper.ToAuditMetadata(entity));
    }
}
