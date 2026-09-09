using Diten.AuthService.Application.Common;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Queries;

/// <summary>F2: returns the legal-entity ids the given user is assigned to (F3 selector consumes this).</summary>
public sealed record GetUserLegalEntitiesQuery(Guid UserId) : IRequest<Response<IReadOnlyList<Guid>>>;
