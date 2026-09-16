using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

/// <summary>getSupplier (contract GET /{supplierId}). Public SupplierId ile; cross-tenant/LE → 404.</summary>
public sealed record GetSupplierByIdQuery(string SupplierId) : IRequest<Response<SupplierDetailDto>>;
