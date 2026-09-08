using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Handlers;

public sealed class EvaluateDataWarehouseLakehouseReadinessHandler : IRequestHandler<EvaluateDataWarehouseLakehouseReadinessCommand, Response<DataWarehouseLakehouseReadinessDto>>
{
    private readonly IDataWarehouseLakehouseReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateDataWarehouseLakehouseReadinessHandler(IDataWarehouseLakehouseReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<DataWarehouseLakehouseReadinessDto>> Handle(EvaluateDataWarehouseLakehouseReadinessCommand request, CancellationToken ct)
    {
        var tenant = DataWarehouseLakehouseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<DataWarehouseLakehouseReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<DataWarehouseLakehouseReadinessDto>.Fail("DataWarehouseLakehouse readiness record was not found.", 404);
        }

        DataWarehouseLakehouseGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<DataWarehouseLakehouseReadinessDto>.Success(DataWarehouseLakehouseMapper.ToDto(entity));
    }
}
