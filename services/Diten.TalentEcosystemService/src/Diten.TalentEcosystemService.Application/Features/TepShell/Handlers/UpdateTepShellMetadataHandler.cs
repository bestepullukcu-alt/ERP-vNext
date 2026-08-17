using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TepShell.Commands;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Handlers;

public sealed class UpdateTepShellMetadataHandler
    : IRequestHandler<UpdateTepShellMetadataCommand, Response<TepShellMetadataDto>>
{
    private readonly ITepShellMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public UpdateTepShellMetadataHandler(ITepShellMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TepShellMetadataDto>> Handle(UpdateTepShellMetadataCommand request, CancellationToken ct)
    {
        var tenant = TepShellGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<TepShellMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }
        var tenantId = tenant.Data;

        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<TepShellMetadataDto>.Fail("TEP shell metadata was not found.", 404);
        }

        var validation = TepShellGuard.ValidateRequest(request.Request);
        if (validation.Count > 0)
        {
            return Response<TepShellMetadataDto>.Fail(validation, 400);
        }

        var dependency = TepShellGuard.ValidateDependencyState(request.Request);
        if (!dependency.IsSuccessful)
        {
            return Response<TepShellMetadataDto>.Fail(dependency.Errors, dependency.StatusCode);
        }

        if (await _repository.ExistsActiveCodeAsync(tenantId, request.Request.Code.Trim(), request.Id, ct))
        {
            return Response<TepShellMetadataDto>.Fail("An active TEP shell metadata record with the same Code already exists for this tenant.", 409);
        }

        entity.Code = request.Request.Code.Trim();
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.ShellState = request.Request.ShellState;
        entity.HcmFoundationState = request.Request.HcmFoundationState;
        entity.PrivacyLegalState = request.Request.PrivacyLegalState;
        entity.ConsentBoundaryState = request.Request.ConsentBoundaryState;
        entity.VisibilityBoundaryState = request.Request.VisibilityBoundaryState;
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.DependencyStates = request.Request.DependencyStates.Select(TepShellMapper.ToEntity).ToList();
        entity.LastEvaluatedAt = request.Request.LastEvaluatedAt;
        entity.ShellVersion = request.Request.ShellVersion;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<TepShellMetadataDto>.Success(TepShellMapper.ToDto(entity));
    }
}
