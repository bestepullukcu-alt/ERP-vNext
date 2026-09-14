using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationOperations.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Handlers;

public sealed class CreateAssociationOperationsReadinessHandler : IRequestHandler<CreateAssociationOperationsReadinessCommand, Response<Guid>>
{
    private readonly IAssociationOperationsReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateAssociationOperationsReadinessHandler(IAssociationOperationsReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateAssociationOperationsReadinessCommand request, CancellationToken ct)
    {
        var tenant = AssociationOperationsGuard.RequireTenant(_tenantContext);
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

        var errors = AssociationOperationsGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = AssociationOperationsGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active association-operations readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = AssociationOperationsGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new AssociationOperationsReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            AssociationOperationsReadinessState = readinessState,
            MembershipCatalogBoundaryState = request.Request.MembershipCatalogBoundaryState,
            ServiceBindingIntakeBoundaryState = request.Request.ServiceBindingIntakeBoundaryState,
            ProgramScopeBoundaryState = request.Request.ProgramScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            OperationsReviewBoundaryState = request.Request.OperationsReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            MemberRegistrySourceDependencyState = request.Request.MemberRegistrySourceDependencyState,
            SectorTrendSourceDependencyState = request.Request.SectorTrendSourceDependencyState,
            DataGovernancePolicyDependencyState = request.Request.DataGovernancePolicyDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, AssociationOperationsReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            AssociationOperationsReadinessVersion = request.Request.AssociationOperationsReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = AssociationOperationsGuard.MergeDeferredReason(
                AssociationOperationsGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == AssociationOperationsReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
