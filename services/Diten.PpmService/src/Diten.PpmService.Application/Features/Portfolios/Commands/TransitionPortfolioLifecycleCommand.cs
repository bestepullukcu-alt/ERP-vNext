using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed record TransitionPortfolioLifecycleCommand(Guid Id, PortfolioLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<PortfolioDto>>;
