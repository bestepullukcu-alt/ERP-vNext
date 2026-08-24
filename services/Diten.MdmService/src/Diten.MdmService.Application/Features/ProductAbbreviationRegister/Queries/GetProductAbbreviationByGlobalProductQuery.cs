using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;

public sealed record GetProductAbbreviationByGlobalProductQuery(Guid GlobalProductId)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>;
