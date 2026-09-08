using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Handlers;

public sealed class DeleteDataWarehouseLakehouseReadinessHandler : IRequestHandler<DeleteDataWarehouseLakehouseReadinessCommand, Response<bool>>
{
    private readonly IDataWarehouseLakehouseReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteDataWarehouseLakehouseReadinessHandler(IDataWarehouseLakehouseReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteDataWarehouseLakehouseReadinessCommand request, CancellationToken ct)
    {
        var tenant = DataWarehouseLakehouseGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("DataWarehouseLakehouse readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.DataWarehouseLakehouseReadinessState = DataWarehouseLakehouseReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
