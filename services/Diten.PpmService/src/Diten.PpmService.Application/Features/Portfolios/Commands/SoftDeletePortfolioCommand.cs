using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed record SoftDeletePortfolioCommand(Guid Id, int ExpectedVersion) : IRequest<Response<NoContent>>;
