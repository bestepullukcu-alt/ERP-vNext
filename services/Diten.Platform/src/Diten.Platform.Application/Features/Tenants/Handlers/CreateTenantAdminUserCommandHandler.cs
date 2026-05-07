using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class CreateTenantAdminUserCommandHandler : IRequestHandler<CreateTenantAdminUserCommand, Response<TenantAdminUserDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateTenantAdminUserCommandHandler(ITenantRegistryRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<TenantAdminUserDto>> Handle(CreateTenantAdminUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<TenantAdminUserDto>.Fail("Tenant not found.", 404);
        }

        TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        if (!TenantAdminUserSupport.TryNormalizeEmail(request.Request.Email, out var email))
        {
            return Response<TenantAdminUserDto>.Fail("Admin user email must be valid.", 400);
        }

        if (tenant.AdminUsers.Any(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<TenantAdminUserDto>.Fail("Admin user email is already registered for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new TenantAdminUser
        {
            Name = TenantAdminUserSupport.NormalizeName(request.Request.Name, email),
            Email = email,
            Status = TenantAdminUserStatus.PendingInvitation,
            UpdatedAt = now
        };

        tenant.AdminUsers.Add(user);
        TenantAdminUserSupport.AddActivity(tenant, "tenant.admin_user.added", $"Admin user '{email}' added.", _currentUser.ActorName, now);
        await _repository.UpdateAsync(tenant, cancellationToken);

        return Response<TenantAdminUserDto>.Success(TenantAdminUserSupport.ToDto(user), 201);
    }
}
