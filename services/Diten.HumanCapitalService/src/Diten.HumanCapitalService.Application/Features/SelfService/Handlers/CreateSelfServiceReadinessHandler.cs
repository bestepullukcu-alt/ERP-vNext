using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SelfService.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SelfService.Handlers;

public sealed class CreateSelfServiceReadinessHandler : IRequestHandler<CreateSelfServiceReadinessCommand, Response<Guid>>
{
    private readonly ISelfServiceReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateSelfServiceReadinessHandler(ISelfServiceReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateSelfServiceReadinessCommand request, CancellationToken ct)
    {
        var tenant = SelfServiceGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = SelfServiceGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = SelfServiceGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active self-service readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = SelfServiceGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new SelfServiceReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            SelfServiceReadinessState = readinessState,
            RequestIntakeBoundaryState = request.Request.RequestIntakeBoundaryState,
            ApprovalRoutingBoundaryState = request.Request.ApprovalRoutingBoundaryState,
            InboxDeliveryBoundaryState = request.Request.InboxDeliveryBoundaryState,
            ProfileSelfUpdateBoundaryState = request.Request.ProfileSelfUpdateBoundaryState,
            DelegationScopeBoundaryState = request.Request.DelegationScopeBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            IdentityDirectoryDependencyState = request.Request.IdentityDirectoryDependencyState,
            HcmCapabilityDependencyState = request.Request.HcmCapabilityDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, SelfServiceReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            SelfServiceReadinessVersion = request.Request.SelfServiceReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = SelfServiceGuard.MergeDeferredReason(
                SelfServiceGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == SelfServiceReadinessState.Deferred
                    ? "Employee and manager self-service readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
