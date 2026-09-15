using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.EmploymentChanges.Queries;

public sealed record GetEmploymentChangeAuditMetadataQuery(Guid Id) : IRequest<Response<EmploymentChangeAuditMetadataDto>>;
