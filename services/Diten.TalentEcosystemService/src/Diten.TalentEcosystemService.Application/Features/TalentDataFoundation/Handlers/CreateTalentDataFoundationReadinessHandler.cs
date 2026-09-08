using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Handlers;

public sealed class CreateTalentDataFoundationReadinessHandler : IRequestHandler<CreateTalentDataFoundationReadinessCommand, Response<Guid>>
{
    private readonly ITalentDataFoundationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateTalentDataFoundationReadinessHandler(ITalentDataFoundationReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateTalentDataFoundationReadinessCommand request, CancellationToken ct)
    {
        var tenant = TalentDataFoundationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = TalentDataFoundationGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = TalentDataFoundationGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active talent-data-foundation readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = TalentDataFoundationGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new TalentDataFoundationReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            TalentDataFoundationReadinessState = readinessState,
            TalentEntityCatalogBoundaryState = request.Request.TalentEntityCatalogBoundaryState,
            DataIngestionBoundaryState = request.Request.DataIngestionBoundaryState,
            IdentityResolutionBoundaryState = request.Request.IdentityResolutionBoundaryState,
            DataQualityBoundaryState = request.Request.DataQualityBoundaryState,
            LineageTrackingBoundaryState = request.Request.LineageTrackingBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            HcmFoundationDependencyState = request.Request.HcmFoundationDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            DependencyStates = new Dictionary<string, TalentDataFoundationReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            TalentDataFoundationReadinessVersion = request.Request.TalentDataFoundationReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = TalentDataFoundationGuard.MergeDeferredReason(
                TalentDataFoundationGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == TalentDataFoundationReadinessState.Deferred
                    ? "Talent data foundation readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
