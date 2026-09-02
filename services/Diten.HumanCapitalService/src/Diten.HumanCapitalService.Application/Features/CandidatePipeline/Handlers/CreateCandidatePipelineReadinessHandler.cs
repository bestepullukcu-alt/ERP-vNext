using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Handlers;

public sealed class CreateCandidatePipelineReadinessHandler
    : IRequestHandler<CreateCandidatePipelineReadinessCommand, Response<Guid>>
{
    private readonly ICandidatePipelineReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateCandidatePipelineReadinessHandler(
        ICandidatePipelineReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateCandidatePipelineReadinessCommand request, CancellationToken ct)
    {
        var tenant = CandidatePipelineGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = CandidatePipelineGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = CandidatePipelineGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active candidate pipeline readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = CandidatePipelineGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new CandidatePipelineReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            PipelineReadinessState = readinessState,
            PipelineStageGovernanceState = request.Request.PipelineStageGovernanceState,
            InterviewSchedulingReadinessState = request.Request.InterviewSchedulingReadinessState,
            InterviewerAssignmentReadinessState = request.Request.InterviewerAssignmentReadinessState,
            EvaluationGovernanceState = request.Request.EvaluationGovernanceState,
            CandidateCommunicationBoundaryState = request.Request.CandidateCommunicationBoundaryState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            CalendarDependencyState = request.Request.CalendarDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            AutomatedDecisionBoundaryState = request.Request.AutomatedDecisionBoundaryState,
            DependencyStates = new Dictionary<string, CandidatePipelineReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            PipelineReadinessVersion = request.Request.PipelineReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = CandidatePipelineGuard.MergeDeferredReason(
                CandidatePipelineGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == CandidatePipelineReadinessState.Deferred
                    ? "Candidate pipeline readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
