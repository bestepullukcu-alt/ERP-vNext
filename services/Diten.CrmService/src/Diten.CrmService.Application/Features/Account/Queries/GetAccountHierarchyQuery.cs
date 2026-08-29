using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Account.Queries;

public sealed record GetAccountHierarchyQuery(Guid Id) : IRequest<Response<AccountHierarchyNodeDto>>;
