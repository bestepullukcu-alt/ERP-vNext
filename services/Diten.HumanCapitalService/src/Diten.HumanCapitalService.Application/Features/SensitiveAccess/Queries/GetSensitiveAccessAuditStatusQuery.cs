using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess.Queries;

public sealed record GetSensitiveAccessAuditStatusQuery() : IRequest<Response<SensitiveAccessAuditStatusDto>>;
