using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Handlers;

public sealed class EvaluateIndustrySuccessionPoolReadinessHandler : IRequestHandler<EvaluateIndustrySuccessionPoolReadinessCommand, Response<IndustrySuccessionPoolReadinessDto>>
{
    private readonly IIndustrySuccessionPoolReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateIndustrySuccessionPoolReadinessHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IndustrySuccessionPoolReadinessDto>> Handle(EvaluateIndustrySuccessionPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySuccessionPoolReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<IndustrySuccessionPoolReadinessDto>.Fail("IndustrySuccessionPool readiness record was not found.", 404);
        }

        IndustrySuccessionPoolGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<IndustrySuccessionPoolReadinessDto>.Success(IndustrySuccessionPoolMapper.ToDto(entity));
    }
}
