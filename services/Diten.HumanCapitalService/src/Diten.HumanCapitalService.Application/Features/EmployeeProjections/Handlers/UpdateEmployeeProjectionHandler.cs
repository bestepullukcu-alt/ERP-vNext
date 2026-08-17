using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Handlers;

public sealed class UpdateEmployeeProjectionHandler : IRequestHandler<UpdateEmployeeProjectionCommand, Response<EmployeeProjectionDto>>
{
    private readonly IEmployeeProjectionRepository _repository;
    private readonly IEmployeeProjectionReferenceValidator _referenceValidator;
    private readonly ITenantContext _tenantContext;

    public UpdateEmployeeProjectionHandler(
        IEmployeeProjectionRepository repository,
        IEmployeeProjectionReferenceValidator referenceValidator,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _referenceValidator = referenceValidator;
        _tenantContext = tenantContext;
    }

    public async Task<Response<EmployeeProjectionDto>> Handle(UpdateEmployeeProjectionCommand request, CancellationToken ct)
    {
        var tenantId = EmployeeProjectionGuards.RequireTenant(_tenantContext);
        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<EmployeeProjectionDto>.Fail("Employee projection was not found.", 404);
        }

        var validationErrors = EmployeeProjectionGuards.ValidateRequest(request.Request);
        if (validationErrors.Count > 0)
        {
            return Response<EmployeeProjectionDto>.Fail(validationErrors, 400);
        }

        var code = EmployeeProjectionGuards.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, request.Id, ct))
        {
            return Response<EmployeeProjectionDto>.Fail("Employee projection code already exists for this tenant.", 409);
        }

        var state = await EmployeeProjectionGuards.ResolveReferenceStateAsync(tenantId, request.Request, _referenceValidator, ct);
        if (!state.IsSuccessful)
        {
            return Response<EmployeeProjectionDto>.Fail(state.Errors, state.StatusCode);
        }

        entity.Code = code;
        entity.DisplayName = request.Request.DisplayName.Trim();
        entity.HrisSourceProfileId = request.Request.HrisSourceProfileId;
        entity.PersonReferenceId = request.Request.PersonReferenceId;
        entity.ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim();
        entity.EmploymentRecordReferenceKey = request.Request.EmploymentRecordReferenceKey.Trim();
        entity.EmploymentStatusCode = request.Request.EmploymentStatusCode.Trim().ToUpperInvariant();
        entity.WorkerTypeCode = request.Request.WorkerTypeCode.Trim().ToUpperInvariant();
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.ProjectionState = state.Data;
        entity.VisibilityClassification = request.Request.VisibilityClassification!.Value;
        entity.SourceLastSyncedAt = request.Request.SourceLastSyncedAt;
        entity.ProjectionVersion = request.Request.ProjectionVersion;
        entity.LastValidatedAt = state.Data == Domain.Enums.EmployeeProjectionState.Validated ? DateTimeOffset.UtcNow : entity.LastValidatedAt;
        entity.DeferredReason = state.Data == Domain.Enums.EmployeeProjectionState.Deferred ? "Reference validation deferred or unavailable." : null;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(entity, ct);
        return Response<EmployeeProjectionDto>.Success(EmployeeProjectionMapper.ToDto(entity));
    }
}
