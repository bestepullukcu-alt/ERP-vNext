using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Handlers;

public sealed class UpdatePositionAssignmentHandler
    : IRequestHandler<UpdatePositionAssignmentCommand, Response<PositionAssignmentDto>>
{
    private readonly IPositionAssignmentOverlayRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly IPositionAssignmentReferenceValidator _referenceValidator;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public UpdatePositionAssignmentHandler(
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

    public async Task<Response<PositionAssignmentDto>> Handle(UpdatePositionAssignmentCommand request, CancellationToken ct)
    {
        var tenant = PositionAssignmentGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<PositionAssignmentDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = PositionAssignmentGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<PositionAssignmentDto>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var entity = await _repository.GetByIdAsync(tenantId, request.Id, ct);
        if (entity is null)
        {
            return Response<PositionAssignmentDto>.Fail("Position assignment overlay was not found.", 404);
        }

        var code = PositionAssignmentGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, request.Id, ct))
        {
            return Response<PositionAssignmentDto>.Fail("An active position assignment with the same Code already exists for this tenant.", 409);
        }

        var employee = await _employeeRepository.GetByIdAsync(tenantId, request.Request.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<PositionAssignmentDto>.Fail("Employee projection anchor was not found.", 404);
        }

        var sensitive = await PositionAssignmentGuard.EvaluateSensitiveAccessAsync(tenantId, employee, _dataScopeEvaluator, ct);
        if (!PositionAssignmentGuard.IsSensitiveAccessAllowedForMutation(sensitive))
        {
            return Response<PositionAssignmentDto>.Fail("Sensitive access precondition did not allow assignment mutation.", 403);
        }

        var reference = await PositionAssignmentGuard.ValidateReferencesAsync(
            tenantId,
            request.Request,
            _employeeRepository,
            _referenceValidator,
            ct);
        if (!reference.IsSuccessful)
        {
            return Response<PositionAssignmentDto>.Fail(reference.Errors, reference.StatusCode);
        }

        var referenceDecision = reference.Data!;
        var now = DateTimeOffset.UtcNow;
        entity.Code = code;
        entity.EmployeeProjectionId = request.Request.EmployeeProjectionId;
        entity.PersonReferenceId = request.Request.PersonReferenceId;
        entity.OrganizationUnitId = request.Request.OrganizationUnitId;
        entity.PositionId = request.Request.PositionId;
        entity.ManagerEmployeeProjectionId = request.Request.ManagerEmployeeProjectionId;
        entity.EffectiveFrom = request.Request.EffectiveFrom;
        entity.EffectiveTo = request.Request.EffectiveTo;
        entity.AssignmentState = referenceDecision.ReferenceState == AssignmentReferenceValidationState.Deferred
            ? AssignmentOverlayState.Deferred
            : request.Request.AssignmentState;
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.ReferenceValidationState = referenceDecision.ReferenceState;
        entity.SensitiveAccessDecisionState = sensitive;
        entity.AssignmentVersion = request.Request.AssignmentVersion;
        entity.LastReferenceValidatedAt = referenceDecision.ReferenceState == AssignmentReferenceValidationState.Validated ? now : entity.LastReferenceValidatedAt;
        entity.DeferredReason = referenceDecision.DeferredReason;
        entity.UpdatedAt = now;

        await _repository.UpdateAsync(entity, ct);
        return Response<PositionAssignmentDto>.Success(PositionAssignmentMapper.ToDto(entity));
    }
}
