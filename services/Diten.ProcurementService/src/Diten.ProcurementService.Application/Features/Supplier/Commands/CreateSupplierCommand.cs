using Diten.BuildingBlocks.CentralAudit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// createSupplier (contract POST /) — "link or create" (K-6): a supplier the tenant does not have yet is created with the
/// caller company's relationship; an existing one (same country + normalized tax number) answers 409 with its code.
/// Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile idempotent. §21: <c>Create</c> · <c>Supplier</c>.
/// </summary>
public sealed record CreateSupplierCommand(
    string Name,
    string? Country,
    string? TaxId,
    List<SupplierContactInput>? Contacts,
    string? SourceSystem,
    string? ExternalRef,
    string? IdempotencyKey) : IRequest<Response<SupplierDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Create", SupplierCentralAudit.SupplierEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}
