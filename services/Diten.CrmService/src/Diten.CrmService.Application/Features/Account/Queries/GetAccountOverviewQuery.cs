using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

public sealed record GetAccountOverviewQuery(Guid Id) : IRequest<Response<AccountOverviewDto>>;
