using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Queries;

public sealed record GetAuditEventByIdQuery(Guid Id) : IRequest<Response<AuditEventDetailDto>>;
