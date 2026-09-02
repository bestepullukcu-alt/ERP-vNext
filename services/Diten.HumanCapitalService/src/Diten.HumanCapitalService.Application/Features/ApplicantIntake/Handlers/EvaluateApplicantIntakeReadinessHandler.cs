using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Handlers;

public sealed class EvaluateApplicantIntakeReadinessHandler
    : IRequestHandler<EvaluateApplicantIntakeReadinessCommand, Response<ApplicantIntakeReadinessDto>>
{
    private readonly IApplicantIntakeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public EvaluateApplicantIntakeReadinessHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<ApplicantIntakeReadinessDto>> Handle(EvaluateApplicantIntakeReadinessCommand request, CancellationToken ct)
    {
        var tenant = ApplicantIntakeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<ApplicantIntakeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<ApplicantIntakeReadinessDto>.Fail("Applicant intake readiness record was not found.", 404);
        }

        ApplicantIntakeGuard.ApplyEvaluation(entity, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(entity, ct);
        return Response<ApplicantIntakeReadinessDto>.Success(ApplicantIntakeMapper.ToDto(entity));
    }
}
