using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ITenantContext _tenantContext;

    public GetUserByIdQueryHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _tenantContext = tenantContext;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (user == null) throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);

        return new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles);
    }
}
