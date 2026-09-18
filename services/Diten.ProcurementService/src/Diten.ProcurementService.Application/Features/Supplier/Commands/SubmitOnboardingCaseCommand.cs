using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// submitOnboardingCase (contract POST /{supplierId}/onboarding). KYC/sanctions çalıştır, doc ekle, onaya gönder.
/// Idempotency-Key ile idempotent. Binary SAKLANMAZ — yalnız evidenceRef (MOD-0029/0031).
/// </summary>
public sealed class SubmitOnboardingCaseCommand : IRequest<Response<OnboardingCaseDto>>
{
    public string SupplierId { get; set; } = string.Empty;
    public KycInput? Kyc { get; set; }
    public List<OnboardingDocumentInput>? Documents { get; set; }
    public bool? SubmitForApproval { get; set; }
    public string? IdempotencyKey { get; set; }
}
