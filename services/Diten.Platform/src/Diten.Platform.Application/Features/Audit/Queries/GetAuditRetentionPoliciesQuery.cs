using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Queries;

public sealed record GetAuditRetentionPoliciesQuery
    : IRequest<Response<IReadOnlyList<AuditRetentionPolicyDto>>>;
