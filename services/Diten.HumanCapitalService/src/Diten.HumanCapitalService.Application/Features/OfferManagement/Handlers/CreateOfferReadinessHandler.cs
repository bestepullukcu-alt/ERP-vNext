using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement.Handlers;

public sealed class CreateOfferReadinessHandler : IRequestHandler<CreateOfferReadinessCommand, Response<Guid>>
{
    private readonly IOfferReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateOfferReadinessHandler(IOfferReadinessMetadataRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreateOfferReadinessCommand request, CancellationToken ct)
    {
        var tenant = OfferManagementGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var errors = OfferManagementGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var code = OfferManagementGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, code, null, ct))
        {
            return Response<Guid>.Fail("An active offer readiness record with the same Code already exists for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var readinessState = OfferManagementGuard.ResolveFailClosedReadinessState(request.Request);
        var entity = new OfferReadinessMetadata
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            OfferReadinessState = readinessState,
            OfferWorkflowBoundaryState = request.Request.OfferWorkflowBoundaryState,
            ApprovalWorkflowBoundaryState = request.Request.ApprovalWorkflowBoundaryState,
            CandidateAcceptanceBoundaryState = request.Request.CandidateAcceptanceBoundaryState,
            OfferDocumentBoundaryState = request.Request.OfferDocumentBoundaryState,
            CompensationDataBoundaryState = request.Request.CompensationDataBoundaryState,
            BenefitsDataBoundaryState = request.Request.BenefitsDataBoundaryState,
            PayrollDataBoundaryState = request.Request.PayrollDataBoundaryState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            DependencyStates = new Dictionary<string, OfferReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            OfferReadinessVersion = request.Request.OfferReadinessVersion,
            LastEvaluatedAt = now,
            DeferredReason = OfferManagementGuard.MergeDeferredReason(
                OfferManagementGuard.NormalizeOptional(request.Request.DeferredReason),
                readinessState == OfferReadinessState.Deferred
                    ? "Offer readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
