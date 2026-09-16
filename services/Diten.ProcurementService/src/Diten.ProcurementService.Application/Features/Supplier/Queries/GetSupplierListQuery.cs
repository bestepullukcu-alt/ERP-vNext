using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

public sealed record GetSupplierListQuery() : IRequest<Response<IReadOnlyList<SupplierListItemDto>>>;
