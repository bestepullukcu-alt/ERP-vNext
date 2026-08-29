using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Queries;

public sealed record GetProductAbbreviationWorkItemsQuery(string Scope, int Limit = 100)
    : IRequest<ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>>;
