using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Commands;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Handlers;

public sealed class DeleteKpiCatalogReadinessHandler : IRequestHandler<DeleteKpiCatalogReadinessCommand, Response<bool>>
{
    private readonly IKpiCatalogReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteKpiCatalogReadinessHandler(IKpiCatalogReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteKpiCatalogReadinessCommand request, CancellationToken ct)
    {
        var tenant = KpiCatalogGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("KpiCatalog readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.KpiCatalogReadinessState = KpiCatalogReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
