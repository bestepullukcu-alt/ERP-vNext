using Diten.BuildingBlocks.CentralAudit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// updateSupplier (contract PATCH /{supplierId}) WITH <c>procurement.suppliers.master.update</c> (§14, K-1): the common
/// fields (tenant-wide — every related company sees the change) and the caller company's relationship fields. Same body and
/// If-Match as <see cref="UpdateSupplierCommand"/>; the controller picks this command when the caller holds the key.
/// §21: <c>Update</c> · <c>Supplier</c>.
/// </summary>
public sealed class UpdateSupplierMasterCommand : IRequest<Response<SupplierDetailDto>>, ICentralAuditDeclaration
{
    public string SupplierId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public List<SupplierContactInput>? Contacts { get; set; }
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
    public int? ExpectedVersion { get; set; }

    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Update", SupplierCentralAudit.SupplierEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}
