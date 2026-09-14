using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Handlers;

public sealed class DeleteApplicantIntakeReadinessHandler
    : IRequestHandler<DeleteApplicantIntakeReadinessCommand, Response<bool>>
{
    private readonly IApplicantIntakeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public DeleteApplicantIntakeReadinessHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<bool>> Handle(DeleteApplicantIntakeReadinessCommand request, CancellationToken ct)
    {
        var tenant = ApplicantIntakeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("Applicant intake readiness record was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.IntakeState = ApplicantIntakeReadinessState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
