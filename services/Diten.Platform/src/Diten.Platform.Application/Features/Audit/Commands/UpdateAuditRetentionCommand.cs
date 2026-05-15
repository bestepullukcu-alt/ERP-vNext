using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Commands;

public sealed record UpdateAuditRetentionCommand(UpdateAuditRetentionRequest Request)
    : IRequest<Response<AuditRetentionPolicyDto>>;
