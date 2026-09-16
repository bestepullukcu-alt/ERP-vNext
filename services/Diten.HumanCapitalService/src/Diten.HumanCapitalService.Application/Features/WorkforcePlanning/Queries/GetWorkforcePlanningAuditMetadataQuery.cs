using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.WorkforcePlanning.Queries;

public sealed record GetWorkforcePlanningAuditMetadataQuery(Guid Id) : IRequest<Response<WorkforcePlanningAuditMetadataDto>>;
