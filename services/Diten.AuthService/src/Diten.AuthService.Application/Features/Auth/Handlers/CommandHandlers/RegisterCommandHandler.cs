using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleProvisioningService _roleProvisioningService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IRoleProvisioningService roleProvisioningService,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _roleProvisioningService = roleProvisioningService;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        if (existing != null)
            throw new InvalidOperationException("E-posta zaten kullanımda.");

        var role = await _roleRepository.GetByNameAndTenantAsync("Viewer", _tenantContext.TenantId, ct);
        if (role is null)
        {
            await _roleProvisioningService.EnsureDefaultRolesAsync(_tenantContext.TenantId, ct);
            role = await _roleRepository.GetByNameAndTenantAsync("Viewer", _tenantContext.TenantId, ct);
        }

        if (role is null)
        {
            _logger.LogWarning("Default role could not be resolved after ensure fallback. TenantId={TenantId}", _tenantContext.TenantId);
            throw new HttpStatusException(422, "Tenant default roles are not ready.");
        }

        var hashedPassword = _passwordHasher.Hash(request.Password);
        var user = new User(request.Email, hashedPassword, request.FirstName, request.LastName, _tenantContext.TenantId);
        var created = await _userRepository.CreateAsync(user, ct);
        await _userRoleRepository.AssignAsync(new UserRole(created.Id, role.Id, _tenantContext.TenantId, "System"), ct);

        var roles = new[] { role.Name };
        var accessToken = _tokenService.GenerateAccessToken(created, roles, Array.Empty<string>());
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken(
            created.Id,
            refreshTokenStr,
            DateTime.UtcNow.AddDays(7),
            "0.0.0.0",
            _tenantContext.TenantId,
            "tenant_user");
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, roles, created.TenantId)
        );
    }
}
