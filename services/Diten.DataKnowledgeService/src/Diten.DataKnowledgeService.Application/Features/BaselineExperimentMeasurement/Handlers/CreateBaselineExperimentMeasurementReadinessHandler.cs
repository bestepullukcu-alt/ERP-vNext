using Diten.DataKnowledgeService.Application.Common;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Commands;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Handlers;

public sealed class CreateBaselineExperimentMeasurementReadinessHandler : IRequestHandler<CreateBaselineExperimentMeasurementReadinessCommand, Response<Guid>>
{
    private readonly IBaselineExperimentMeasurementReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateBaselineExperimentMeasurementReadinessHandler(IBaselineExperimentMeasurementReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateBaselineExperimentMeasurementReadinessCommand request, CancellationToken ct)
    {
        var tenant = BaselineExperimentMeasurementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = BaselineExperimentMeasurementGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = BaselineExperimentMeasurementGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active baseline-experiment-measurement readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = BaselineExperimentMeasurementGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new BaselineExperimentMeasurementReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            BaselineExperimentMeasurementReadinessState = readinessState,
            BaselineCatalogBoundaryState = request.Request.BaselineCatalogBoundaryState,
            ExperimentDesignIntakeBoundaryState = request.Request.ExperimentDesignIntakeBoundaryState,
            MeasurementBindingScopeBoundaryState = request.Request.MeasurementBindingScopeBoundaryState,
            ResultPublicationControlBoundaryState = request.Request.ResultPublicationControlBoundaryState,
            ExperimentReviewBoundaryState = request.Request.ExperimentReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            MetricSemanticRegistrySourceDependencyState = request.Request.MetricSemanticRegistrySourceDependencyState,
            ScorecardSourceDependencyState = request.Request.ScorecardSourceDependencyState,
            DataContractRegistryDependencyState = request.Request.DataContractRegistryDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            StewardshipPreconditionState = request.Request.StewardshipPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            MeasurementPolicyState = request.Request.MeasurementPolicyState,
            DependencyStates = new Dictionary<string, BaselineExperimentMeasurementReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            BaselineExperimentMeasurementReadinessVersion = request.Request.BaselineExperimentMeasurementReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = BaselineExperimentMeasurementGuard.MergeDeferredReason(
                BaselineExperimentMeasurementGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == BaselineExperimentMeasurementReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
