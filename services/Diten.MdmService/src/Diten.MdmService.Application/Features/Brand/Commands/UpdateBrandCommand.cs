using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Commands;

public sealed record UpdateBrandCommand(Guid BrandId, BrandWriteRequest Request, string? Actor = null)
    : IRequest<Response<NoContent>>;
