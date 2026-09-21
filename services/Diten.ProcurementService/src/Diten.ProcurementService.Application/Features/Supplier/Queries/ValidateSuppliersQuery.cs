using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

/// <summary>
/// validateSuppliers (contract POST /validate). Toplu public id doğrulama; bilinmeyen id → known:false
/// (consumer fail-closed). Tenant+LE filtreli — başka tenant/LE'nin id'si de known:false döner (sızıntı yok).
/// </summary>
public sealed record ValidateSuppliersQuery(IReadOnlyList<string> SupplierIds)
    : IRequest<Response<SupplierValidateResponseDto>>;
