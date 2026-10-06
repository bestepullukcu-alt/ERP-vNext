using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// updateSupplier (contract PATCH /{supplierId}). Optimistic concurrency: <see cref="ExpectedVersion"/> If-Match
/// header'dan gelir; stale → 409 ConcurrencyConflict (sessiz overwrite YOK). SupplierId route'tan set edilir.
/// </summary>
public sealed class UpdateSupplierCommand : IRequest<Response<SupplierDetailDto>>
{
    public string SupplierId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public List<SupplierContactInput>? Contacts { get; set; }
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    /// <summary>If-Match rowVersion; null ise concurrency zorlanmaz (mevcut Version kullanılır).</summary>
    public int? ExpectedVersion { get; set; }
}
