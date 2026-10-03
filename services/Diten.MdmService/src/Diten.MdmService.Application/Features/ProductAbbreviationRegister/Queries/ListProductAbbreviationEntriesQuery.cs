using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;

/// <summary>FIX1 B2 — every register entry of one Global Product (requested, active, closed), for its register page.</summary>
public sealed record ListProductAbbreviationEntriesQuery(Guid GlobalProductId)
    : IRequest<Response<IReadOnlyList<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterListItemDto>>>;
