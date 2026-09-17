using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed record GetPortfolioOwnerCandidatesQuery(Guid Id, string Search = "", int Limit = 20)
    : IRequest<Response<IReadOnlyList<PortfolioOwnerCandidate>>>;
