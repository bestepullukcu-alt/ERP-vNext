using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

/// <summary>listSuppliers (contract GET /). Tenant+LE filtreli; opsiyonel status filtresi + cursor sayfalama.</summary>
public sealed record GetSupplierListQuery(
    SupplierStatus? Status = null,
    string? Cursor = null) : IRequest<Response<SupplierListResultDto>>;
