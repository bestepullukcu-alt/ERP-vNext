using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;

public sealed record ResolveProductAbbreviationQuery(string Abbreviation)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>>;
