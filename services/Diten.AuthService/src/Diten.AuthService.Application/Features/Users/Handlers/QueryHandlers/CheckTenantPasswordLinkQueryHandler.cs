using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Application.Features.Users.Services;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

public sealed class CheckTenantPasswordLinkQueryHandler : IRequestHandler<CheckTenantPasswordLinkQuery, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;

    public CheckTenantPasswordLinkQueryHandler(IUserRepository userRepository, IRefreshTokenHasher refreshTokenHasher)
    {
        _userRepository = userRepository;
        _refreshTokenHasher = refreshTokenHasher;
    }

    public async Task<Response<NoContent>> Handle(CheckTenantPasswordLinkQuery request, CancellationToken ct)
    {
        var (state, _, _) = await TenantPasswordLink.ReadAsync(_userRepository, _refreshTokenHasher, request.Email, request.Token, ct);
        return state switch
        {
            TenantPasswordLinkState.Usable => Response<NoContent>.Success(204),
            TenantPasswordLinkState.Deactivated => TenantPasswordLink.DeactivatedAnswer(),
            _ => TenantPasswordLink.InvalidAnswer()
        };
    }
}
