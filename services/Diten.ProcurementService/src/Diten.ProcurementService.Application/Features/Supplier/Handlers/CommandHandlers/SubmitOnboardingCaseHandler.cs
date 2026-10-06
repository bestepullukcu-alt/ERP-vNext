using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

public sealed class SubmitOnboardingCaseHandler : IRequestHandler<SubmitOnboardingCaseCommand, Response<OnboardingCaseDto>>
{
    private readonly ISupplierRepository _repository;

    public SubmitOnboardingCaseHandler(ISupplierRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<OnboardingCaseDto>> Handle(SubmitOnboardingCaseCommand request, CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE → null → 404 (UNKNOWN_SUPPLIER).
        var entity = await _repository.GetBySupplierIdAsync(request.SupplierId, cancellationToken);
        if (entity is null)
        {
            return Response<OnboardingCaseDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        // ── Idempotent onboarding (MOD-0140 §8): aynı key ile replay → yeniden işlenmez, mevcut case döner ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(entity.OnboardingIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<OnboardingCaseDto>.Success(SupplierMapping.ToOnboardingCase(entity));
        }

        var expectedVersion = entity.Version;
        var submitForApproval = request.SubmitForApproval ?? false;

        // ── Dokümanlar (evidenceRef only; binary SAKLANMAZ) — verildiyse set eder ──
        if (request.Documents is not null)
        {
            entity.Documents = request.Documents
                .Select(d => new SupplierDocument { Type = d.Type, EvidenceRef = d.EvidenceRef })
                .ToList();
        }

        // ── KYC / sanctions outcome (MVP deterministik değerlendirme; gerçek entegrasyon follow-up) ──
        if (request.Kyc is not null)
        {
            entity.KycOutcome = string.IsNullOrWhiteSpace(request.Kyc.LegalName)
                ? KycOutcome.ManualReview
                : KycOutcome.Passed;
            entity.SanctionsOutcome = SanctionsOutcome.Clear;
        }

        // ── Onaya gönderim → InReview + MOD-0023 workflow instance referansı ──
        if (submitForApproval)
        {
            entity.OnboardingStatus = OnboardingStatus.InReview;
            entity.Approval = new OnboardingApproval
            {
                WorkflowInstanceId = entity.Approval?.WorkflowInstanceId ?? GenerateWorkflowInstanceId(),
                DecidedBy = null,
                DecidedAt = null
            };
        }
        else if (entity.OnboardingStatus == OnboardingStatus.Draft)
        {
            entity.OnboardingStatus = OnboardingStatus.Draft;
        }

        entity.OnboardingIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var ok = await _repository.UpdateAsync(entity, expectedVersion, cancellationToken);
        if (!ok)
        {
            // Onboarding contract'ında 409 yok; eşzamanlı değişim → işlenemez (VALIDATION_FAILED 422).
            return Response<OnboardingCaseDto>.Fail("VALIDATION_FAILED", 422);
        }

        return Response<OnboardingCaseDto>.Success(SupplierMapping.ToOnboardingCase(entity));
    }

    private static string GenerateWorkflowInstanceId()
        => "WF-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
}
