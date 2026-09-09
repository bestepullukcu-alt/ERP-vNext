using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.ApplicantIntake.Handlers;

public sealed class CreateApplicantIntakeReadinessHandler
    : IRequestHandler<CreateApplicantIntakeReadinessCommand, Response<Guid>>
{
    private readonly IApplicantIntakeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public CreateApplicantIntakeReadinessHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<Guid>> Handle(CreateApplicantIntakeReadinessCommand request, CancellationToken ct)
    {
        var tenant = ApplicantIntakeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<Guid>.Fail(tenant.Errors, tenant.StatusCode);
        }

        // Fail closed: a write must target a legal entity the caller is entitled to act on.
        if (!await _legalEntityContext.IsSelectionAllowedAsync(ct))
        {
            return Response<Guid>.Fail(
                "A permitted legal entity must be selected (X-Legal-Entity-Id) to create an applicant intake readiness record.",
                403);
        }

        var errors = ApplicantIntakeGuard.Validate(request.Request);
        if (errors.Count > 0)
        {
            return Response<Guid>.Fail(errors, 400);
        }

        var tenantId = tenant.Data;
        var legalEntityId = _legalEntityContext.SelectedLegalEntityId!.Value;
        var code = ApplicantIntakeGuard.NormalizeCode(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(tenantId, legalEntityId, code, null, ct))
        {
            return Response<Guid>.Fail("An active applicant intake readiness record with the same Code already exists for this legal entity.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var intakeState = ApplicantIntakeGuard.ResolveFailClosedIntakeState(request.Request);
        var entity = new ApplicantIntakeReadinessMetadata
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            Code = code,
            DisplayName = request.Request.DisplayName.Trim(),
            IntakeState = intakeState,
            SourceChannelState = request.Request.SourceChannelState,
            ConsentPreconditionState = request.Request.ConsentPreconditionState,
            DataMinimizationState = request.Request.DataMinimizationState,
            DuplicateHandlingState = request.Request.DuplicateHandlingState,
            RetentionPolicyState = request.Request.RetentionPolicyState,
            EvidencePolicyState = request.Request.EvidencePolicyState,
            ApplicantIdentityBoundaryState = request.Request.ApplicantIdentityBoundaryState,
            PublicUxBoundaryState = request.Request.PublicUxBoundaryState,
            DocumentDependencyState = request.Request.DocumentDependencyState,
            NotificationDependencyState = request.Request.NotificationDependencyState,
            DependencyStates = new Dictionary<string, ApplicantIntakeReadinessState>(request.Request.DependencyStates),
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            ApplicantIntakeVersion = request.Request.ApplicantIntakeVersion,
            LastEvaluatedAt = now,
            DeferredReason = ApplicantIntakeGuard.MergeDeferredReason(
                ApplicantIntakeGuard.NormalizeOptional(request.Request.DeferredReason),
                intakeState == ApplicantIntakeReadinessState.Deferred
                    ? "Applicant intake readiness is deferred until required metadata preconditions are satisfied."
                    : null),
            CreatedAt = now
        };

        await _repository.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
