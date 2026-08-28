using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;

public sealed record IssueServiceIdentityTokenCommand(
    string ClientCode,
    string ClientSecret,
    Guid TenantId,
    string Audience) : IRequest<Response<ServiceIdentityTokenResponse>>;
