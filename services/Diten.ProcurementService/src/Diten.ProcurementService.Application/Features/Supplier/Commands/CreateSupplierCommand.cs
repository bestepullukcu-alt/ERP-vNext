using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// createSupplier (contract POST /). Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile idempotent.
/// </summary>
public sealed record CreateSupplierCommand(
    string Name,
    string? Country,
    string? TaxId,
    List<SupplierContactInput>? Contacts,
    string? SourceSystem,
    string? ExternalRef,
    string? IdempotencyKey) : IRequest<Response<SupplierDetailDto>>;
