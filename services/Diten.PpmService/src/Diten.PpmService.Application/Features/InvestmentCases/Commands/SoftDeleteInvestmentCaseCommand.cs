using Diten.PpmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.InvestmentCases;

public sealed record SoftDeleteInvestmentCaseCommand(Guid Id, int ExpectedVersion) : IRequest<Response<NoContent>>;
