using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HeadcountBudget.Queries;

public sealed record GetHeadcountBudgetAuditMetadataQuery(Guid Id) : IRequest<Response<HeadcountBudgetAuditMetadataDto>>;
