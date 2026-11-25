using Diten.BuildingBlocks.CentralAudit;
using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Commands;

/// <summary>
/// 1.4.0 <c>linkSupplierToCompany</c> (POST /{supplierId}/relationship; K-1): the caller company starts a relationship with
/// a supplier the tenant already has (the "link to this company" path after a 409, K-6 — an explicit company decision, never
/// automatic). Idempotency-Key; an existing relationship → 409 <c>RELATIONSHIP_EXISTS</c>. Only under <c>TenantShared</c>.
/// §21: <c>Assign</c> · <c>SupplierCompanyRelationship</c>.
/// </summary>
public sealed record LinkSupplierToCompanyCommand(string SupplierId, string? IdempotencyKey)
    : IRequest<Response<SupplierRelationshipDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Assign", SupplierCentralAudit.RelationshipEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}

/// <summary>
/// 1.4.0 <c>changeRelationshipStatus</c> (PUT /{supplierId}/relationship/status; If-Match): the caller company's relationship
/// status (Active / OnHold / Blocked / Inactive). §21: <c>LifecycleTransition</c> · <c>SupplierCompanyRelationship</c>.
/// </summary>
public sealed record ChangeRelationshipStatusCommand(string SupplierId, SupplierStatus Status, int? ExpectedVersion)
    : IRequest<Response<SupplierRelationshipDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("LifecycleTransition", SupplierCentralAudit.RelationshipEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}

/// <summary>
/// 1.4.0 <c>setRelationshipPaymentTerms</c> (PUT /{supplierId}/relationship/payment-terms; If-Match): the caller company's
/// payment-terms CODE (K-8; no bank data). §21: <c>Update</c> · <c>SupplierCompanyRelationship</c>.
/// </summary>
public sealed record SetRelationshipPaymentTermsCommand(string SupplierId, string? PaymentTermsCode, int? ExpectedVersion)
    : IRequest<Response<SupplierRelationshipDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => SupplierCentralAudit.Of("Update", SupplierCentralAudit.RelationshipEntity);

    public Guid? CentralAuditEntityId(object? response) => null;
}
