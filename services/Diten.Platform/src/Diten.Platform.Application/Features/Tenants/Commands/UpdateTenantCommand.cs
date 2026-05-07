using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantCommand(Guid TenantId, TenantUpdateRequest Request) : IRequest<Response<TenantDetailDto>>;
