using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Handlers;

public sealed class CreateEmploymentChangeReadinessHandler : IRequestHandler<CreateEmploymentChangeReadinessCommand, Response<Guid>>
{
    private readonly IEmploymentChangeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateEmploymentChangeReadinessHandler(IEmploymentChangeReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateEmploymentChangeReadinessCommand request, CancellationToken ct)
    {
        var tenant = EmploymentChangeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = EmploymentChangeGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = EmploymentChangeGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active employment change readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = EmploymentChangeGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new EmploymentChangeReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            EmploymentChangeReadinessState = readinessState,
            ChangeLifecycleBoundaryState = request.Request.ChangeLifecycleBoundaryState,
            TransferBoundaryState = request.Request.TransferBoundaryState,
            PromotionBoundaryState = request.Request.PromotionBoundaryState,
            ApprovalBoundaryState = request.Request.ApprovalBoundaryState,
            PositionAssignmentBoundaryState = request.Request.PositionAssignmentBoundaryState,
            EmployeeActionBoundaryState = request.Request.EmployeeActionBoundaryState,
            ManagerActionBoundaryState = request.Request.ManagerActionBoundaryState,
            CompensationDataBoundaryState = request.Request.CompensationDataBoundaryState,
            BenefitsDataBoundaryState = request.Request.BenefitsDataBoundaryState,
            PayrollDataBoundaryState = request.Request.PayrollDataBoundaryState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, EmploymentChangeReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            EmploymentChangeReadinessVersion = request.Request.EmploymentChangeReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = EmploymentChangeGuard.MergeDeferredReason(
                EmploymentChangeGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == EmploymentChangeReadinessState.Deferred
                    ? "Employment change readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
