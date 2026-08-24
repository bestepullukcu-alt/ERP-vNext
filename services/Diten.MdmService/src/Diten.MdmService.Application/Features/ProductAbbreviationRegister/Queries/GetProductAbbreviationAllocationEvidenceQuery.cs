using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;

public sealed record GetProductAbbreviationAllocationEvidenceQuery(Guid RegisterEntryId)
    : IRequest<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>>;
