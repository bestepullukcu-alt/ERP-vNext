using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Queries;

public sealed record GetBrandByIdQuery(Guid BrandId) : IRequest<Response<BrandDetailDto>>;
