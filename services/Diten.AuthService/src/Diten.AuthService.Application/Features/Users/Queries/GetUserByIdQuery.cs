using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.DTOs;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<Response<UserDto>>;
