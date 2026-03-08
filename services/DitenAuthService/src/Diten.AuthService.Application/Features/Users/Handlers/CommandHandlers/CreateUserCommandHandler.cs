using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var existing = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        if (existing != null) throw new InvalidOperationException("E-posta zaten kullanımda.");

        var hashedPassword = _passwordHasher.Hash(request.Password);
        var user = new User(request.Email, hashedPassword, request.FirstName, request.LastName, _tenantContext.TenantId);

        var created = await _userRepository.CreateAsync(user, ct);
        
        _logger.LogInformation("Kullanıcı oluşturuldu. Id={Id}", created.Id);

        return new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, new List<string>());
    }
}
