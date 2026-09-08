using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Queries;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Handlers;

public sealed class GetBaselineExperimentMeasurementReadinessListHandler
    : IRequestHandler<GetBaselineExperimentMeasurementReadinessListQuery, Response<IReadOnlyList<BaselineExperimentMeasurementReadinessListItemDto>>>
{
    private readonly IBaselineExperimentMeasurementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetBaselineExperimentMeasurementReadinessListHandler(IBaselineExperimentMeasurementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<BaselineExperimentMeasurementReadinessListItemDto>>> Handle(
        GetBaselineExperimentMeasurementReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = BaselineExperimentMeasurementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<BaselineExperimentMeasurementReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var rows = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<BaselineExperimentMeasurementReadinessListItemDto>>.Success(rows.Select(BaselineExperimentMeasurementMapper.ToListItem).ToList());
    }
}

public sealed class GetBaselineExperimentMeasurementReadinessByIdHandler
    : IRequestHandler<GetBaselineExperimentMeasurementReadinessByIdQuery, Response<BaselineExperimentMeasurementReadinessDto>>
{
    private readonly IBaselineExperimentMeasurementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetBaselineExperimentMeasurementReadinessByIdHandler(IBaselineExperimentMeasurementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<BaselineExperimentMeasurementReadinessDto>> Handle(GetBaselineExperimentMeasurementReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = BaselineExperimentMeasurementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<BaselineExperimentMeasurementReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<BaselineExperimentMeasurementReadinessDto>.Fail("BaselineExperimentMeasurement readiness record was not found.", 404)
            : Response<BaselineExperimentMeasurementReadinessDto>.Success(BaselineExperimentMeasurementMapper.ToDto(entity));
    }
}

public sealed class GetBaselineExperimentMeasurementAuditMetadataHandler
    : IRequestHandler<GetBaselineExperimentMeasurementAuditMetadataQuery, Response<BaselineExperimentMeasurementAuditMetadataDto>>
{
    private readonly IBaselineExperimentMeasurementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetBaselineExperimentMeasurementAuditMetadataHandler(IBaselineExperimentMeasurementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<BaselineExperimentMeasurementAuditMetadataDto>> Handle(GetBaselineExperimentMeasurementAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = BaselineExperimentMeasurementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<BaselineExperimentMeasurementAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        return entity is null
            ? Response<BaselineExperimentMeasurementAuditMetadataDto>.Fail("BaselineExperimentMeasurement readiness record was not found.", 404)
            : Response<BaselineExperimentMeasurementAuditMetadataDto>.Success(BaselineExperimentMeasurementMapper.ToAuditMetadata(entity));
    }
}
