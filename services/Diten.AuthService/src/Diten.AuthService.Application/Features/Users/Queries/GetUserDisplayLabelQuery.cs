using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>
/// WP-INFRA-AUTH-DISPLAY-LABEL-01 — "what name should a consumer show for this id": tenant membership (a user of
/// another tenant is a 404, byte-identical to a missing one) plus a display label. Guarded by <c>auth.users.lookup</c>.
/// </summary>
public sealed record GetUserDisplayLabelQuery(Guid UserId) : IRequest<Response<UserDisplayLabelDto>>;
