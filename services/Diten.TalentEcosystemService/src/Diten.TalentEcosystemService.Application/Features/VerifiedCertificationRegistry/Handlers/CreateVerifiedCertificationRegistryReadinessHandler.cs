using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Handlers;

public sealed class CreateVerifiedCertificationRegistryReadinessHandler : IRequestHandler<CreateVerifiedCertificationRegistryReadinessCommand, Response<Guid>>
{
    private readonly IVerifiedCertificationRegistryReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateVerifiedCertificationRegistryReadinessHandler(IVerifiedCertificationRegistryReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateVerifiedCertificationRegistryReadinessCommand request, CancellationToken ct)
    {
        var tenant = VerifiedCertificationRegistryGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create this record.",
                403);
        }

        var errors = VerifiedCertificationRegistryGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = VerifiedCertificationRegistryGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active verified-certification-registry readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = VerifiedCertificationRegistryGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new VerifiedCertificationRegistryReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            VerifiedCertificationRegistryReadinessState = readinessState,
            CertificationCatalogBoundaryState = request.Request.CertificationCatalogBoundaryState,
            VerificationIntakeBoundaryState = request.Request.VerificationIntakeBoundaryState,
            IssuerBindingScopeBoundaryState = request.Request.IssuerBindingScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            RegistryReviewBoundaryState = request.Request.RegistryReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            SkillPassportSourceDependencyState = request.Request.SkillPassportSourceDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, VerifiedCertificationRegistryReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            VerifiedCertificationRegistryReadinessVersion = request.Request.VerifiedCertificationRegistryReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = VerifiedCertificationRegistryGuard.MergeDeferredReason(
                VerifiedCertificationRegistryGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == VerifiedCertificationRegistryReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
