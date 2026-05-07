using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record DeleteTenantCommand(Guid TenantId) : IRequest<Response<NoContent>>;
