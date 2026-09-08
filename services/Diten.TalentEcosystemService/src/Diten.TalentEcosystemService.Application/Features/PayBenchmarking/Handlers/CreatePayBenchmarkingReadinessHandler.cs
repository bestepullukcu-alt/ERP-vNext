using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Handlers;

public sealed class CreatePayBenchmarkingReadinessHandler : IRequestHandler<CreatePayBenchmarkingReadinessCommand, Response<Guid>>
{
    private readonly IPayBenchmarkingReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreatePayBenchmarkingReadinessHandler(IPayBenchmarkingReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePayBenchmarkingReadinessCommand request, CancellationToken ct)
    {
        var tenant = PayBenchmarkingGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = PayBenchmarkingGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = PayBenchmarkingGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active salary-benchmarking readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = PayBenchmarkingGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new PayBenchmarkingReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            PayBenchmarkingReadinessState = readinessState,
            ReferenceRangeCatalogBoundaryState = request.Request.ReferenceRangeCatalogBoundaryState,
            ContributionIntakeBoundaryState = request.Request.ContributionIntakeBoundaryState,
            AggregationScopeBoundaryState = request.Request.AggregationScopeBoundaryState,
            VisibilityControlBoundaryState = request.Request.VisibilityControlBoundaryState,
            BenchmarkingReviewBoundaryState = request.Request.BenchmarkingReviewBoundaryState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            TalentDataSourceDependencyState = request.Request.TalentDataSourceDependencyState,
            ConsentPolicyDependencyState = request.Request.ConsentPolicyDependencyState,
            DataGovernancePolicyDependencyState = request.Request.DataGovernancePolicyDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            PublicationPolicyState = request.Request.PublicationPolicyState,
            DependencyStates = new Dictionary<string, PayBenchmarkingReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            PayBenchmarkingReadinessVersion = request.Request.PayBenchmarkingReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = PayBenchmarkingGuard.MergeDeferredReason(
                PayBenchmarkingGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == PayBenchmarkingReadinessState.Deferred
                    ? "Professional reputation ledger readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
