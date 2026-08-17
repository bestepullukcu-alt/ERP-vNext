using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        ILogger<DeleteUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        await _userRepository.SoftDeleteAsync(request.Id, _tenantContext.TenantId, ct);
        _logger.LogInformation("User soft-deleted. Id={Id}", request.Id);
        return Response<NoContent>.Success(204);
    }
}
