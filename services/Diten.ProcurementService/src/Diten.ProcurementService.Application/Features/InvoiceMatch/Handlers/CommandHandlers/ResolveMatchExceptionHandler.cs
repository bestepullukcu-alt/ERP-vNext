using Diten.ProcurementService.Application.Features.InvoiceMatch.Commands;
using Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;
using Diten.ProcurementService.Application.Interfaces;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using InvoiceMatchResultEnum = Diten.ProcurementService.Domain.Entities.InvoiceMatchResult;
using InvoiceStatusEnum = Diten.ProcurementService.Domain.Entities.InvoiceStatus;
using MatchExceptionStatusEnum = Diten.ProcurementService.Domain.Entities.MatchExceptionStatus;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Handlers.CommandHandlers;

/// <summary>
/// resolveMatchException — approve/reject/tolerance-override. Exception'ı Resolved yapar (approval trail: decision +
/// note + resolvedBy [ICurrentUserContext] + resolvedAt) ve ilgili faturanın durumunu günceller:
/// approve → ClearedForPayment (ÖDEME YÜRÜTMEZ — yalnız eşleşme-durumu işareti; AP/payment=Finance/Treasury),
/// tolerance-override → MatchedWithinTolerance, reject → Rejected. Idempotent (Idempotency-Key → replay ikinci geçiş
/// YOK). Kapalı (Resolved) exception resolve → 409 INVALID_STATE. Cross-tenant/LE → 404.
/// </summary>
public sealed class ResolveMatchExceptionHandler : IRequestHandler<ResolveMatchExceptionCommand, Response<MatchOutcomeDto>>
{
    private readonly IInvoiceMatchRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ResolveMatchExceptionHandler(IInvoiceMatchRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<MatchOutcomeDto>> Handle(ResolveMatchExceptionCommand request, CancellationToken cancellationToken)
    {
        var exception = await _repository.GetExceptionByExceptionIdAsync(request.ExceptionId, cancellationToken);
        if (exception is null)
        {
            return Response<MatchOutcomeDto>.Fail("NOT_FOUND", 404);
        }

        var invoice = await _repository.GetByInvoiceIdAsync(exception.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Response<MatchOutcomeDto>.Fail("NOT_FOUND", 404);
        }

        // ── Idempotent replay: aynı resolve key + zaten Resolved → mevcut outcome döner (ikinci geçiş YOK) ──
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && exception.Status == MatchExceptionStatusEnum.Resolved
            && string.Equals(exception.ResolveIdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Response<MatchOutcomeDto>.Success(InvoiceMatchMapping.ToOutcomeDto(invoice));
        }

        // ── Kapalı exception resolve edilemez → 409 INVALID_STATE (sessiz overwrite YOK) ──
        if (exception.Status != MatchExceptionStatusEnum.Open)
        {
            return Response<MatchOutcomeDto>.Fail("INVALID_STATE", 409);
        }

        var decision = request.Decision?.Trim().ToLowerInvariant();
        if (!InvoiceMatchValidationRules.IsValidDecision(decision))
        {
            return Response<MatchOutcomeDto>.Fail("VALIDATION_FAILED", 422);
        }

        // ── Karara göre invoice durumu + outcome result (ÖDEME YÜRÜTÜLMEZ) ──
        InvoiceMatchResultEnum outcomeResult;
        switch (decision)
        {
            case InvoiceMatchValidationRules.DecisionApprove:
                invoice.Status = InvoiceStatusEnum.ClearedForPayment; // yalnız işaret; ödeme Finance/Treasury
                outcomeResult = InvoiceMatchResultEnum.MatchedWithinTolerance;
                break;
            case InvoiceMatchValidationRules.DecisionToleranceOverride:
                invoice.Status = InvoiceStatusEnum.MatchedWithinTolerance;
                outcomeResult = InvoiceMatchResultEnum.MatchedWithinTolerance;
                break;
            default: // reject
                invoice.Status = InvoiceStatusEnum.Rejected;
                outcomeResult = InvoiceMatchResultEnum.Exception;
                break;
        }

        var resolveKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        // ── Approval trail (MOD-0023 seam) ──
        exception.Status = MatchExceptionStatusEnum.Resolved;
        exception.ResolutionDecision = decision;
        exception.ResolutionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note!.Trim();
        exception.ResolvedBy = _currentUser.UserName;
        exception.ResolvedAt = DateTimeOffset.UtcNow;
        exception.ResolveIdempotencyKey = resolveKey;

        var exceptionUpdated = await _repository.UpdateExceptionAsync(exception, exception.Version, cancellationToken);
        if (!exceptionUpdated)
        {
            return Response<MatchOutcomeDto>.Fail("INVALID_STATE", 409);
        }

        invoice.LastMatchResult = outcomeResult;
        // reject → exception kaydı bilgi olarak açık kalmaz ama invoice.ExceptionId referansı çözüldü işaretlenir.
        invoice.ExceptionId = decision == InvoiceMatchValidationRules.DecisionReject ? invoice.ExceptionId : null;

        var invoiceUpdated = await _repository.UpdateAsync(invoice, invoice.Version, cancellationToken);
        if (!invoiceUpdated)
        {
            return Response<MatchOutcomeDto>.Fail("INVALID_STATE", 409);
        }

        return Response<MatchOutcomeDto>.Success(InvoiceMatchMapping.ToOutcomeDto(invoice));
    }
}
