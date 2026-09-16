using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Supplier.Queries;

public sealed record GetSupplierByIdQuery(Guid Id) : IRequest<Response<SupplierDetailDto>>;
