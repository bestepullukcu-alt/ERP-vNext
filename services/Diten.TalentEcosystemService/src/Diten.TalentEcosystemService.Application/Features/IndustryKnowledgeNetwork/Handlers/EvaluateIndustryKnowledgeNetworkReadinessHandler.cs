using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Handlers;

public sealed class EvaluateIndustryKnowledgeNetworkReadinessHandler : IRequestHandler<EvaluateIndustryKnowledgeNetworkReadinessCommand, Response<IndustryKnowledgeNetworkReadinessDto>>
{
    private readonly IIndustryKnowledgeNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateIndustryKnowledgeNetworkReadinessHandler(IIndustryKnowledgeNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustryKnowledgeNetworkReadinessDto>> Handle(EvaluateIndustryKnowledgeNetworkReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustryKnowledgeNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustryKnowledgeNetworkReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<IndustryKnowledgeNetworkReadinessDto>.Fail("IndustryKnowledgeNetwork readiness record was not found.", 404);
        }

        IndustryKnowledgeNetworkGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<IndustryKnowledgeNetworkReadinessDto>.Success(IndustryKnowledgeNetworkMapper.ToDto(entity));
    }
}
