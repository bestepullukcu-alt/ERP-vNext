using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

public sealed record AssignPlanToTenantCommand(Guid TenantId, AssignPlanToTenantRequest Request)
    : IRequest<Response<Guid>>;
