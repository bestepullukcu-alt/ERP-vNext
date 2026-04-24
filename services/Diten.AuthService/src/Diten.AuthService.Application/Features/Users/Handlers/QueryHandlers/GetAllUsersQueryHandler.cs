using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PaginatedResult<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ITenantContext _tenantContext;

    public GetAllUsersQueryHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _tenantContext = tenantContext;
    }

    public async Task<PaginatedResult<UserDto>> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        var users = await _userRepository.GetAllByTenantAsync(_tenantContext.TenantId, request.Page, request.PageSize, ct);
        var total = await _userRepository.GetCountByTenantAsync(_tenantContext.TenantId, ct);

        var dtos = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);
            dtos.Add(new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId));
        }

        return new PaginatedResult<UserDto>(dtos, total, request.Page, request.PageSize);
    }
}
