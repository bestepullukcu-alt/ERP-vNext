using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationOperations.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Handlers;

public sealed class EvaluateAssociationOperationsReadinessHandler : IRequestHandler<EvaluateAssociationOperationsReadinessCommand, Response<AssociationOperationsReadinessDto>>
{
    private readonly IAssociationOperationsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateAssociationOperationsReadinessHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AssociationOperationsReadinessDto>> Handle(EvaluateAssociationOperationsReadinessCommand request, CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<AssociationOperationsReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<AssociationOperationsReadinessDto>.Fail("AssociationOperations readiness record was not found.", 404);
        }

        AssociationOperationsGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<AssociationOperationsReadinessDto>.Success(AssociationOperationsMapper.ToDto(entity));
    }
}
