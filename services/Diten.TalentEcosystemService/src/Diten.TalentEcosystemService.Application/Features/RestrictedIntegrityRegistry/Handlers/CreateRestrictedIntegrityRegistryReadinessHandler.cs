using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RestrictedIntegrityRegistry.Handlers;

public sealed class CreateRestrictedIntegrityRegistryReadinessHandler : IRequestHandler<CreateRestrictedIntegrityRegistryReadinessCommand, Response<Guid>>
{
    private readonly IRestrictedIntegrityRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateRestrictedIntegrityRegistryReadinessHandler(IRestrictedIntegrityRegistryReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateRestrictedIntegrityRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = RestrictedIntegrityRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = RestrictedIntegrityRegistryGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = RestrictedIntegrityRegistryGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active restricted-integrity-registry readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = RestrictedIntegrityRegistryGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new RestrictedIntegrityRegistryReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            RestrictedIntegrityRegistryReadinessState = readinessState,
            IntegrityCaseCatalogBoundaryState = request.Request.IntegrityCaseCatalogBoundaryState,
            RestrictionScopeBoundaryState = request.Request.RestrictionScopeBoundaryState,
            EvidenceChainBoundaryState = request.Request.EvidenceChainBoundaryState,
            DisclosureControlBoundaryState = request.Request.DisclosureControlBoundaryState,
            CaseReviewBoundaryState = request.Request.CaseReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            EarlyWarningSourceDependencyState = request.Request.EarlyWarningSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            LegalHoldDependencyState = request.Request.LegalHoldDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, RestrictedIntegrityRegistryReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            RestrictedIntegrityRegistryReadinessVersion = request.Request.RestrictedIntegrityRegistryReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = RestrictedIntegrityRegistryGuard.MergeDeferredReason(
                RestrictedIntegrityRegistryGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == RestrictedIntegrityRegistryReadinessState.Deferred
                    ? "Restricted integrity registry readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
