using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;

public sealed class CreateCandidateDisputeReadinessHandler : IRequestHandler<CreateCandidateDisputeReadinessCommand, Response<Guid>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateCandidateDisputeReadinessHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateCandidateDisputeReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var validation = CandidateDisputeGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var readiness = CandidateDisputeGuard.ValidateReadinessRequest(request.Request);
        if (!readiness.IsSuccessful)
        {
            return Response<Guid>.Fail(readiness.Errors, readiness.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenant.Data, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active candidate dispute readiness record with the same Code already exists for this tenant.", 409);
        }

        var entity = CandidateDisputeHandlerMapper.ToEntity(tenant.Data, request.Request);
        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
