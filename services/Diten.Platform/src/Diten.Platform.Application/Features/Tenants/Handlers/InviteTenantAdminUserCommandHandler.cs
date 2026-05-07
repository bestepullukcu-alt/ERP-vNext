using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class InviteTenantAdminUserCommandHandler : IRequestHandler<InviteTenantAdminUserCommand, TenantAdminUserDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAdminUserInvitationService _invitationService;

    public InviteTenantAdminUserCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IAdminUserInvitationService invitationService)
    {
        _repository = repository;
        _currentUser = currentUser;
        _invitationService = invitationService;
    }

    public async Task<TenantAdminUserDto?> Handle(InviteTenantAdminUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        var user = tenant.AdminUsers.FirstOrDefault(item => item.Id == request.AdminUserId);
        if (user == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var invitation = await _invitationService.InviteAsync(tenant, user, cancellationToken);
        user.Status = TenantAdminUserStatus.Invited;
        user.InvitedAt = now;
        user.UpdatedAt = now;
        TenantAdminUserSupport.AddActivity(
            tenant,
            "tenant.admin_user.invited",
            $"Admin invitation sent for '{user.Email}'. Login: {invitation.LoginUrl}",
            _currentUser.ActorName,
            now);
        await _repository.UpdateAsync(tenant, cancellationToken);

        return TenantAdminUserSupport.ToDto(user);
    }
}
