using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Commands;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Handlers;

public sealed class DeleteEtlEltPipelinesReadinessHandler : IRequestHandler<DeleteEtlEltPipelinesReadinessCommand, Response<bool>>
{
    private readonly IEtlEltPipelinesReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public DeleteEtlEltPipelinesReadinessHandler(IEtlEltPipelinesReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(DeleteEtlEltPipelinesReadinessCommand request, CancellationToken ct)
    {
        var tenant = EtlEltPipelinesGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("EtlEltPipelines readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.EtlEltPipelinesReadinessState = EtlEltPipelinesReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
