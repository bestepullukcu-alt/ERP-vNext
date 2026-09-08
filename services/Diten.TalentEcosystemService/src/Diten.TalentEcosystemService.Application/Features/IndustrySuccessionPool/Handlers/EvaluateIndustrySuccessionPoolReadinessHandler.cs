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

    public EvaluateIndustrySuccessionPoolReadinessHandler(IIndustrySuccessionPoolReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IndustrySuccessionPoolReadinessDto>> Handle(EvaluateIndustrySuccessionPoolReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustrySuccessionPoolGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IndustrySuccessionPoolReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<IndustrySuccessionPoolReadinessDto>.Fail("IndustrySuccessionPool readiness record was not found.", 404);
        }

        IndustrySuccessionPoolGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<IndustrySuccessionPoolReadinessDto>.Success(IndustrySuccessionPoolMapper.ToDto(entity));
    }
}
