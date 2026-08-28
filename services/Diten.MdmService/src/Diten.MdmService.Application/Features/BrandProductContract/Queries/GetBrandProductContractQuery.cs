using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.BrandProductContract.Queries;

public sealed record GetBrandProductContractQuery : IRequest<Response<BrandProductContractDto>>;
