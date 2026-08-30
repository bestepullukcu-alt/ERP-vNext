using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record TransitionInvestmentCaseLifecycleCommand(Guid Id, InvestmentCaseLifecycleState TargetState, int ExpectedVersion) : IRequest<Response<InvestmentCaseDto>>;
