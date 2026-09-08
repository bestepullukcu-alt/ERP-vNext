using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Handlers;

public sealed class CreateIndustryKnowledgeNetworkReadinessHandler : IRequestHandler<CreateIndustryKnowledgeNetworkReadinessCommand, Response<Guid>>
{
    private readonly IIndustryKnowledgeNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateIndustryKnowledgeNetworkReadinessHandler(IIndustryKnowledgeNetworkReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateIndustryKnowledgeNetworkReadinessCommand request, CancellationToken ct)
    {
        var tenant = IndustryKnowledgeNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = IndustryKnowledgeNetworkGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = IndustryKnowledgeNetworkGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active industry-knowledge-network readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = IndustryKnowledgeNetworkGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new IndustryKnowledgeNetworkReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            IndustryKnowledgeNetworkReadinessState = readinessState,
            KnowledgeCatalogBoundaryState = request.Request.KnowledgeCatalogBoundaryState,
            ContentBindingIntakeBoundaryState = request.Request.ContentBindingIntakeBoundaryState,
            NetworkScopeBoundaryState = request.Request.NetworkScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            KnowledgeReviewBoundaryState = request.Request.KnowledgeReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            KnowledgeSourceRegistryDependencyState = request.Request.KnowledgeSourceRegistryDependencyState,
            SectorTrendSourceDependencyState = request.Request.SectorTrendSourceDependencyState,
            DataGovernancePolicyDependencyState = request.Request.DataGovernancePolicyDependencyState,
            AssociationOperationsSourceDependencyState = request.Request.AssociationOperationsSourceDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, IndustryKnowledgeNetworkReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            IndustryKnowledgeNetworkReadinessVersion = request.Request.IndustryKnowledgeNetworkReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = IndustryKnowledgeNetworkGuard.MergeDeferredReason(
                IndustryKnowledgeNetworkGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == IndustryKnowledgeNetworkReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
