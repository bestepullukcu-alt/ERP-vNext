using Diten.BuildingBlocks.CentralAudit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// Soft delete of the caller company's relationship (public SupplierId); the tenant-wide supplier stays. Hard delete YOK
/// (MOD-0140 §8). §21: <c>Delete</c> · <c>SupplierCompanyRelationship</c>.
/// </summary>
public sealed record DeleteSupplierCommand(string SupplierId) : IRequest<Response<bool>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Delete", SupplierCentralAudit.RelationshipEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}
