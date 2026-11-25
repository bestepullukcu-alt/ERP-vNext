using Diten.BuildingBlocks.CentralAudit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// updateSupplier (contract PATCH /{supplierId}) WITHOUT <c>procurement.suppliers.master.update</c> (§14, K-1): only the
/// caller company's relationship fields change (<c>sourceSystem</c>, <c>externalRef</c>). Under <c>TenantShared</c> a body
/// that changes a common field (name, country, tax number, contacts) is refused with 403 <c>MASTER_UPDATE_FORBIDDEN</c> and
/// nothing is written; under <c>PerLegalEntity</c> each company has its own record and the common fields change as today.
/// Optimistic concurrency: <see cref="ExpectedVersion"/> If-Match header'dan gelir; stale → 409. §21: <c>Update</c> ·
/// <c>SupplierCompanyRelationship</c>.
/// </summary>
public sealed class UpdateSupplierCommand : IRequest<Response<SupplierDetailDto>>, ICentralAuditDeclaration
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

    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Update", SupplierCentralAudit.RelationshipEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}
