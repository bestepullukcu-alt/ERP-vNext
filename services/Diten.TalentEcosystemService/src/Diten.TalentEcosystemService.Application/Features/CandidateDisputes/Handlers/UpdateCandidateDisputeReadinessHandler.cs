using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;

public sealed class UpdateCandidateDisputeReadinessHandler : IRequestHandler<UpdateCandidateDisputeReadinessCommand, Response<NoContent>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public UpdateCandidateDisputeReadinessHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(UpdateCandidateDisputeReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<NoContent>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<NoContent>.Fail("Candidate dispute readiness record was not found.", 404);
        }

        var validation = CandidateDisputeGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<NoContent>.Fail(validation, 400);
        }

        var readiness = CandidateDisputeGuard.ValidateReadinessRequest(request.Request);
        if (!readiness.IsSuccessful)
        {
            return Response<NoContent>.Fail(readiness.Errors, readiness.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenant.Data, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<NoContent>.Fail("An active candidate dispute readiness record with the same Code already exists for this tenant.", 409);
        }

        CandidateDisputeHandlerMapper.Apply(entity, request.Request);
        await _repository.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
