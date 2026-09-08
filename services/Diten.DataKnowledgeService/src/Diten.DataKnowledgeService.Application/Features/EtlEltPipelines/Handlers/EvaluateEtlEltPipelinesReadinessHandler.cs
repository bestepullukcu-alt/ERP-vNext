using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Commands;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Handlers;

public sealed class EvaluateEtlEltPipelinesReadinessHandler : IRequestHandler<EvaluateEtlEltPipelinesReadinessCommand, Response<EtlEltPipelinesReadinessDto>>
{
    private readonly IEtlEltPipelinesReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateEtlEltPipelinesReadinessHandler(IEtlEltPipelinesReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EtlEltPipelinesReadinessDto>> Handle(EvaluateEtlEltPipelinesReadinessCommand request, CancellationToken ct)
    {
        var tenant = EtlEltPipelinesGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<EtlEltPipelinesReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<EtlEltPipelinesReadinessDto>.Fail("EtlEltPipelines readiness record was not found.", 404);
        }

        EtlEltPipelinesGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<EtlEltPipelinesReadinessDto>.Success(EtlEltPipelinesMapper.ToDto(entity));
    }
}
