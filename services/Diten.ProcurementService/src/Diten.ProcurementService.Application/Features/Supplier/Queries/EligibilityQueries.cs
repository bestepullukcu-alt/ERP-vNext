using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

/// <summary>
/// getSupplierEligibility (1.3.0, GET /{supplierId}/eligibility; §3.1): the path value is a code (canonical, then the caller
/// company's legacy code) or the <c>supplierMasterId</c> uuid. No relationship in the caller's company → 404 UNKNOWN_SUPPLIER.
/// </summary>
public sealed record GetSupplierEligibilityQuery(string SupplierId) : IRequest<Response<SupplierEligibilityDto>>;

/// <summary>listEligibleSuppliers (1.3.0, GET /eligible; §3.1, F-2): exactly the caller company's eligible relationships, by code.</summary>
public sealed record ListEligibleSuppliersQuery(string? Cursor) : IRequest<Response<EligibleSupplierListDto>>;

/// <summary>getSupplierRelationship (1.4.0, GET /{supplierId}/relationship): the caller company's relationship.</summary>
public sealed record GetSupplierRelationshipQuery(string SupplierId) : IRequest<Response<SupplierRelationshipDto>>;
