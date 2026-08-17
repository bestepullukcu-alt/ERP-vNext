using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Handlers;

public sealed class CreatePositionAssignmentHandler
    : IRequestHandler<CreatePositionAssignmentCommand, Response<Guid>>
{
    private readonly IPositionAssignmentOverlayRepository _repository;
    private readonly IEmployeeProjectionRepository _employeeRepository;
    private readonly IPositionAssignmentReferenceValidator _referenceValidator;
    private readonly ISensitiveAccessDataScopeEvaluator _dataScopeEvaluator;
    private readonly ITenantContext _tenantContext;

    public CreatePositionAssignmentHandler(
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

    public async Task<Response<Guid>> Handle(CreatePositionAssignmentCommand request, CancellationToken ct)
    {
        var tenant = PositionAssignmentGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = PositionAssignmentGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = PositionAssignmentGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active position assignment with the same Code already exists for this tenant.", 409);
        }

        var employee = await _employeeRepository.GetByIdAsync(tenantId, request.Request.EmployeeProjectionId, ct);
        if (employee is null)
        {
            return Response<Guid>.Fail("Employee projection anchor was not found.", 404);
        }

        var sensitive = await PositionAssignmentGuard.EvaluateSensitiveAccessAsync(tenantId, employee, _dataScopeEvaluator, ct);
        if (!PositionAssignmentGuard.IsSensitiveAccessAllowedForMutation(sensitive))
        {
            return Response<Guid>.Fail("Sensitive access precondition did not allow assignment mutation.", 403);
        }

        var reference = await PositionAssignmentGuard.ValidateReferencesAsync(
            tenantId,
            request.Request,
            _employeeRepository,
            _referenceValidator,
            ct);
        if (!reference.IsSuccessful)
        {
            return Response<Guid>.Fail(reference.Errors, reference.StatusCode);
        }

        var referenceDecision = reference.Data!;
        var now = DateTimeOffset.UtcNow;
        var assignmentState = ResolveAssignmentState(request.Request.AssignmentState, referenceDecision.ReferenceState);
        var entity = new EmployeePositionAssignmentOverlay
        {
            TenantId = tenantId,
            Code = code,
            EmployeeProjectionId = request.Request.EmployeeProjectionId,
            PersonReferenceId = request.Request.PersonReferenceId,
            OrganizationUnitId = request.Request.OrganizationUnitId,
            PositionId = request.Request.PositionId,
            ManagerEmployeeProjectionId = request.Request.ManagerEmployeeProjectionId,
            EffectiveFrom = request.Request.EffectiveFrom,
            EffectiveTo = request.Request.EffectiveTo,
            AssignmentState = assignmentState,
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            ReferenceValidationState = referenceDecision.ReferenceState,
            SensitiveAccessDecisionState = sensitive,
            AssignmentVersion = request.Request.AssignmentVersion,
            LastReferenceValidatedAt = referenceDecision.ReferenceState == AssignmentReferenceValidationState.Validated ? now : null,
            DeferredReason = referenceDecision.DeferredReason,
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }

    private static AssignmentOverlayState ResolveAssignmentState(
        AssignmentOverlayState requested,
        AssignmentReferenceValidationState referenceState) =>
        referenceState == AssignmentReferenceValidationState.Deferred
            ? AssignmentOverlayState.Deferred
            : requested;
}
