using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeProjections.Handlers;

public sealed class CreateEmployeeProjectionHandler : IRequestHandler<CreateEmployeeProjectionCommand, Response<Guid>>
{
    private readonly IEmployeeProjectionRepository _repository;
    private readonly IEmployeeProjectionReferenceValidator _referenceValidator;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateEmployeeProjectionHandler(
        IEmployeeProjectionRepository repository,
        IEmployeeProjectionReferenceValidator referenceValidator,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _referenceValidator = referenceValidator;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateEmployeeProjectionCommand request, CancellationToken ct)
    {
        var tenantId = EmployeeProjectionGuards.RequireTenant(_tenantContext);

        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create this record.",
                403);
        }

        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var validationErrors = EmployeeProjectionGuards.ValidateRequest(request.Request);
        if (validationErrors.Count > 0)
        {
            return Response<Guid>.Fail(validationErrors, 400);
        }

        var code = EmployeeProjectionGuards.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("Employee projection code already exists for this legal entity.", 409);
        }

        var state = await EmployeeProjectionGuards.ResolveReferenceStateAsync(tenantId, request.Request, _referenceValidator, ct);
        if (!state.IsSuccessful)
        {
            return Response<Guid>.Fail(state.Errors, state.StatusCode);
        }

        var entity = new EmployeeProfileProjection
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            HrisSourceProfileId = request.Request.HrisSourceProfileId,
            PersonReferenceId = request.Request.PersonReferenceId,
            ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim(),
            EmploymentRecordReferenceKey = request.Request.EmploymentRecordReferenceKey.Trim(),
            EmploymentStatusCode = request.Request.EmploymentStatusCode.Trim().ToUpperInvariant(),
            WorkerTypeCode = request.Request.WorkerTypeCode.Trim().ToUpperInvariant(),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            ProjectionState = state.Data,
            VisibilityClassification = request.Request.VisibilityClassification!.Value,
            SourceLastSyncedAt = request.Request.SourceLastSyncedAt,
            ProjectionVersion = request.Request.ProjectionVersion,
            LastValidatedAt = state.Data == Domain.Enums.EmployeeProjectionState.Validated ? DateTimeOffset.UtcNow : null,
            DeferredReason = state.Data == Domain.Enums.EmployeeProjectionState.Deferred ? "Reference validation deferred or unavailable." : null
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
