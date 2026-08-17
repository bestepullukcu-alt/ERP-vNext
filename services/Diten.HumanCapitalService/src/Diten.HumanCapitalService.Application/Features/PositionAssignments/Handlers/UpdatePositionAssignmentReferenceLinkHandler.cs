using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Handlers;

public sealed class UpdatePositionAssignmentReferenceLinkHandler
    : IRequestHandler<UpdatePositionAssignmentReferenceLinkCommand, Response<PositionAssignmentDto>>
{
    private readonly IPositionAssignmentOverlayRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly IPositionAssignmentReferenceValidator _referenceValidator;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public UpdatePositionAssignmentReferenceLinkHandler(
        IPositionAssignmentOverlayRepository repository,
        IEmployeeProjectionRepository employeeRepository,
        IPositionAssignmentReferenceValidator referenceValidator,
        ISensitiveAccessDataScopeEvaluator dataScopeEvaluator,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _referenceValidator = referenceValidator;
        _dataScopeEvaluator = dataScopeEvaluator;
        _tenantContext = tenantContext;
    }

    public async Task<Response<PositionAssignmentDto>> Handle(UpdatePositionAssignmentReferenceLinkCommand request, CancellationToken ct)
    {
        var tenant = PositionAssignmentGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PositionAssignmentDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var tenantId = tenant.Data;
        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<PositionAssignmentDto>.Fail("Position assignment overlay was not found.", 404);
        }

        var employee = await _employeeRepository.GetByIdAsync(tenantId, entity.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<PositionAssignmentDto>.Fail("Employee projection anchor was not found.", 404);
        }

        var sensitive = await PositionAssignmentGuard.EvaluateSensitiveAccessAsync(tenantId, employee, _dataScopeEvaluator, ct);
        if (!PositionAssignmentGuard.IsSensitiveAccessAllowedForMutation(sensitive))
        {
            return Response<PositionAssignmentDto>.Fail("Sensitive access precondition did not allow assignment reference-link mutation.", 403);
        }

        var validationRequest = new PositionAssignmentCreateRequest
        {
            Code = entity.Code,
            EmployeeProjectionId = entity.EmployeeProjectionId,
            PersonReferenceId = request.Request.PersonReferenceId,
            OrganizationUnitId = request.Request.OrganizationUnitId,
            PositionId = request.Request.PositionId,
            ManagerEmployeeProjectionId = entity.ManagerEmployeeProjectionId,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            AssignmentState = entity.AssignmentState,
            SourceContractVersion = request.Request.SourceContractVersion,
            AssignmentVersion = entity.AssignmentVersion
        };

        var errors = PositionAssignmentGuard.Validate(validationRequest);
        if (errors.Count > 0)
        {
            return Response<PositionAssignmentDto>.Fail(errors, 400);
        }

        var reference = await PositionAssignmentGuard.ValidateReferencesAsync(
            tenantId,
            validationRequest,
            _employeeRepository,
            _referenceValidator,
            ct);
        if (!reference.IsSuccessful)
        {
            return Response<PositionAssignmentDto>.Fail(reference.Errors, reference.StatusCode);
        }

        var referenceDecision = reference.Data!;
        var now = DateTimeOffset.UtcNow;
        entity.PersonReferenceId = request.Request.PersonReferenceId;
        entity.OrganizationUnitId = request.Request.OrganizationUnitId;
        entity.PositionId = request.Request.PositionId;
        entity.AssignmentState = DeriveReferenceLinkState(entity.AssignmentState, referenceDecision.ReferenceState);
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.ReferenceValidationState = referenceDecision.ReferenceState;
        entity.SensitiveAccessDecisionState = sensitive;
        entity.LastReferenceValidatedAt = referenceDecision.ReferenceState == AssignmentReferenceValidationState.Validated ? now : entity.LastReferenceValidatedAt;
        entity.DeferredReason = referenceDecision.DeferredReason;
        entity.UpdatedAt = now;

        await _repository.UpdateAsync(entity, ct);
        return Response<PositionAssignmentDto>.Success(PositionAssignmentMapper.ToDto(entity));
    }

    private static AssignmentOverlayState DeriveReferenceLinkState(
        AssignmentOverlayState current,
        AssignmentReferenceValidationState referenceState)
    {
        if (referenceState == AssignmentReferenceValidationState.Deferred)
        {
            return AssignmentOverlayState.Deferred;
        }

        return current == AssignmentOverlayState.Deferred
            ? AssignmentOverlayState.Validated
            : current;
    }
}
