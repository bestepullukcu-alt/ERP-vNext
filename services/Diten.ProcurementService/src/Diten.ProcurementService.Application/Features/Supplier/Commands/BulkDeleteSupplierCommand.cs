using Diten.BuildingBlocks.CentralAudit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// Toplu soft delete of the caller company's relationships (public SupplierId listesi). Silinen adedini döner. One command,
/// one central record (§21: <c>Delete</c> · <c>SupplierCompanyRelationship</c>).
/// </summary>
public sealed record BulkDeleteSupplierCommand(List<string> SupplierIds) : IRequest<Response<int>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Delete", SupplierCentralAudit.RelationshipEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}
