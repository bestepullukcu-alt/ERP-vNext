using Diten.Shared.Core;
using Diten.ProcurementService.Application.Features.Supplier.Commands;
using Diten.ProcurementService.Application.Features.Supplier.Store;
using Diten.ProcurementService.Domain.Entities;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Handlers.CommandHandlers;

/// <summary>
/// submitOnboardingCase — behaviour unchanged by DL-3 (the real MOD-0023 workflow and its central record are DL-4, §7;
/// the command stays UNMARKED until then — the one unmarked write command of <c>Features.Supplier</c>, §16). The screening
/// result and documents are the supplier's (K-3: one result per firm); the onboarding status and approval are the caller
/// company's relationship.
/// </summary>
public sealed class SubmitOnboardingCaseHandler(ISupplierStore store) : IRequestHandler<SubmitOnboardingCaseCommand, Response<OnboardingCaseDto>>
{
    public async Task<Response<OnboardingCaseDto>> Handle(SubmitOnboardingCaseCommand request, CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE / no relationship → 404 (UNKNOWN_SUPPLIER).
        var current = await store.FindAsync(request.SupplierId, cancellationToken);
        if (current is null)
        {
            return Response<OnboardingCaseDto>.Fail("UNKNOWN_SUPPLIER", 404);
        }

        // ── Idempotent onboarding (MOD-0140 §8): aynı key ile replay → yeniden işlenmez, mevcut case döner ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && string.Equals(current.OnboardingIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<OnboardingCaseDto>.Success(SupplierMapping.ToOnboardingCase(current));
        }

        // ── Dokümanlar (evidenceRef only; binary SAKLANMAZ) — verildiyse set eder ──
        if (request.Documents is not null)
        {
            current.Documents = request.Documents
                .Select(d => new SupplierDocument { Type = d.Type, EvidenceRef = d.EvidenceRef })
                .ToList();
        }

        // ── KYC / sanctions outcome (MVP deterministik değerlendirme; gerçek entegrasyon follow-up) ──
        if (request.Kyc is not null)
        {
            current.KycOutcome = string.IsNullOrWhiteSpace(request.Kyc.LegalName) ? KycOutcome.ManualReview : KycOutcome.Passed;
            current.SanctionsOutcome = SanctionsOutcome.Clear;
        }

        // ── Onaya gönderim → InReview + workflow instance referansı (the MOD-0023 client is DL-4) ──
        if (request.SubmitForApproval ?? false)
        {
            current.OnboardingStatus = OnboardingStatus.InReview;
            current.Approval = new SupplierRelationshipApproval
            {
                WorkflowInstanceId = current.Approval?.WorkflowInstanceId ?? GenerateWorkflowInstanceId(),
                SubmissionRound = current.Approval?.SubmissionRound
            };
        }

        current.OnboardingIdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        var saved = await store.SaveMasterAsync(current, cancellationToken);
        if (saved is null)
        {
            // Onboarding contract'ında 409 yok; eşzamanlı değişim → işlenemez (VALIDATION_FAILED 422).
            return Response<OnboardingCaseDto>.Fail("VALIDATION_FAILED", 422);
        }

        return Response<OnboardingCaseDto>.Success(SupplierMapping.ToOnboardingCase(saved));
    }

    private static string GenerateWorkflowInstanceId()
        => "WF-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
}
