using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Handlers;

public sealed class CreateEmployeeOnboardingReadinessHandler : IRequestHandler<CreateEmployeeOnboardingReadinessCommand, Response<Guid>>
{
    private readonly IEmployeeOnboardingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateEmployeeOnboardingReadinessHandler(IEmployeeOnboardingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateEmployeeOnboardingReadinessCommand request, CancellationToken ct)
    {
        var tenant = EmployeeOnboardingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = EmployeeOnboardingGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = EmployeeOnboardingGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active employee onboarding readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = EmployeeOnboardingGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new EmployeeOnboardingReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            OnboardingReadinessState = readinessState,
            LifecycleBoundaryState = request.Request.LifecycleBoundaryState,
            ChecklistBoundaryState = request.Request.ChecklistBoundaryState,
            ManagerActionBoundaryState = request.Request.ManagerActionBoundaryState,
            EmployeeActionBoundaryState = request.Request.EmployeeActionBoundaryState,
            CandidateTransitionBoundaryState = request.Request.CandidateTransitionBoundaryState,
            IdentityProvisioningBoundaryState = request.Request.IdentityProvisioningBoundaryState,
            AccessProvisioningBoundaryState = request.Request.AccessProvisioningBoundaryState,
            DeviceEquipmentProvisioningBoundaryState = request.Request.DeviceEquipmentProvisioningBoundaryState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, EmployeeOnboardingReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            OnboardingReadinessVersion = request.Request.OnboardingReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = EmployeeOnboardingGuard.MergeDeferredReason(
                EmployeeOnboardingGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == EmployeeOnboardingReadinessState.Deferred
                    ? "Employee onboarding readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
