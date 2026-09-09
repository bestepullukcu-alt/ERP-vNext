using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Handlers;

public sealed class EvaluateHrDocumentationReadinessHandler : IRequestHandler<EvaluateHrDocumentationReadinessCommand, Response<HrDocumentationReadinessDto>>
{
    private readonly IHrDocumentationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public EvaluateHrDocumentationReadinessHandler(IHrDocumentationReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<HrDocumentationReadinessDto>> Handle(EvaluateHrDocumentationReadinessCommand request, CancellationToken ct)
    {
        var tenant = HrDocumentationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<HrDocumentationReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<HrDocumentationReadinessDto>.Fail("HrDocumentation readiness record was not found.", 404);
        }

        HrDocumentationGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<HrDocumentationReadinessDto>.Success(HrDocumentationMapper.ToDto(entity));
    }
}
