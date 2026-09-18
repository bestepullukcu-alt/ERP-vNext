using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class InvoiceMatchMapping
{
    public static InvoiceDto ToDto(Invoice e) => new(
        e.Id,
        e.InvoiceId,
        e.SupplierId,
        e.PoId,
        e.InvoiceNumber,
        e.Status,
        e.Currency,
        e.Lines.Select(l => new InvoiceLineDto(l.PoLineId, l.ItemId, l.Quantity, l.UnitPrice, l.LineAmount)).ToList(),
        e.TotalAmount,
        e.Version,
        InvoiceMatchContract.Version);

    /// <summary>MatchOutcome projeksiyonu — Invoice'un son eşleştirme durumundan (result/variances/grnIds/exceptionId).</summary>
    public static MatchOutcomeDto ToOutcomeDto(Invoice e) => new(
        e.InvoiceId,
        e.PoId,
        e.GrnIds.ToList(),
        e.LastMatchResult ?? InvoiceMatchResult.Exception,
        e.ToleranceProfileId,
        e.Variances.Select(v => new VarianceDto(v.PoLineId, v.Field, v.Expected, v.Actual, v.WithinTolerance)).ToList(),
        e.ExceptionId,
        InvoiceMatchContract.Version);

    public static MatchExceptionDto ToExceptionDto(MatchException e) => new(
        e.ExceptionId,
        e.InvoiceId,
        e.PoId,
        e.ReasonCode,
        e.Status,
        InvoiceMatchContract.Version);
}
