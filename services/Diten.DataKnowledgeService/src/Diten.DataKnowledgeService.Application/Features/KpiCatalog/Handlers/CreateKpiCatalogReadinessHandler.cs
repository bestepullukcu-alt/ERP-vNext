using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.KpiCatalog.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Handlers;

public sealed class CreateKpiCatalogReadinessHandler : IRequestHandler<CreateKpiCatalogReadinessCommand, Response<Guid>>
{
    private readonly IKpiCatalogReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateKpiCatalogReadinessHandler(IKpiCatalogReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateKpiCatalogReadinessCommand request, CancellationToken ct)
    {
        var tenant = KpiCatalogGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = KpiCatalogGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = KpiCatalogGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active kpi-catalog readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = KpiCatalogGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new KpiCatalogReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            KpiCatalogReadinessState = readinessState,
            KpiIdentityCatalogBoundaryState = request.Request.KpiIdentityCatalogBoundaryState,
            DefinitionBindingIntakeBoundaryState = request.Request.DefinitionBindingIntakeBoundaryState,
            OwnershipScopeBoundaryState = request.Request.OwnershipScopeBoundaryState,
            PublicationControlBoundaryState = request.Request.PublicationControlBoundaryState,
            CatalogReviewBoundaryState = request.Request.CatalogReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = request.Request.MetricSemanticRegistrySourceDependencyState,
            DataDictionaryDependencyState = request.Request.DataDictionaryDependencyState,
            DataContractRegistryDependencyState = request.Request.DataContractRegistryDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, KpiCatalogReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            KpiCatalogReadinessVersion = request.Request.KpiCatalogReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = KpiCatalogGuard.MergeDeferredReason(
                KpiCatalogGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == KpiCatalogReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
