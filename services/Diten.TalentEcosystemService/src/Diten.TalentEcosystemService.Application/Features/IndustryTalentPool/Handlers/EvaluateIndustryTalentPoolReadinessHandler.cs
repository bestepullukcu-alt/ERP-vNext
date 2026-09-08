using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Handlers;

public sealed class EvaluateIndustryTalentPoolReadinessHandler : IRequestHandler<EvaluateIndustryTalentPoolReadinessCommand, Response<IndustryTalentPoolReadinessDto>>
{
    private readonly IIndustryTalentPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateIndustryTalentPoolReadinessHandler(IIndustryTalentPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustryTalentPoolReadinessDto>> Handle(EvaluateIndustryTalentPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustryTalentPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustryTalentPoolReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<IndustryTalentPoolReadinessDto>.Fail("IndustryTalentPool readiness record was not found.", 404);
        }

        IndustryTalentPoolGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<IndustryTalentPoolReadinessDto>.Success(IndustryTalentPoolMapper.ToDto(entity));
    }
}
