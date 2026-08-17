using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TepShell.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Handlers;

public sealed class CreateTepShellMetadataHandler
    : IRequestHandler<CreateTepShellMetadataCommand, Response<Guid>>
{
    private readonly ITepShellMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateTepShellMetadataHandler(ITepShellMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateTepShellMetadataCommand request, CancellationToken ct)
    {
        var tenant = TepShellGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var validation = TepShellGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<Guid>.Fail(validation, 400);
        }

        var dependency = TepShellGuard.ValidateDependencyState(request.Request);
        if (!dependency.IsSuccessful)
        {
            return Response<Guid>.Fail(dependency.Errors, dependency.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, request.Request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("An active TEP shell metadata record with the same Code already exists for this tenant.", 409);
        }

        var entity = new TepShellMetadata
        {
            TenantId = tenantId,
            Code = request.Request.Code.Trim(),
            DisplayName = request.Request.DisplayName.Trim(),
            ShellState = request.Request.ShellState,
            HcmFoundationState = request.Request.HcmFoundationState,
            PrivacyLegalState = request.Request.PrivacyLegalState,
            ConsentBoundaryState = request.Request.ConsentBoundaryState,
            VisibilityBoundaryState = request.Request.VisibilityBoundaryState,
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            DependencyStates = request.Request.DependencyStates.Select(TepShellMapper.ToEntity).ToList(),
            LastEvaluatedAt = request.Request.LastEvaluatedAt,
            ShellVersion = request.Request.ShellVersion
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
