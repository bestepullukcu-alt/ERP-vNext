using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class UpdateTenantAdminUserCommandHandler : IRequestHandler<UpdateTenantAdminUserCommand, Response<TenantAdminUserDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTenantAdminUserCommandHandler(ITenantRegistryRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<TenantAdminUserDto>> Handle(UpdateTenantAdminUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<TenantAdminUserDto>.Fail("Tenant not found.", 404);
        }

        var initialAdminAdded = TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        var user = tenant.AdminUsers.FirstOrDefault(item => item.Id == request.AdminUserId);
        if (user == null)
        {
            return Response<TenantAdminUserDto>.Fail("Admin user not found.", 404);
        }

        if (!TenantAdminUserSupport.TryNormalizeEmail(request.Request.Email, out var email))
        {
            return Response<TenantAdminUserDto>.Fail("Admin user email must be valid.", 400);
        }

        if (tenant.AdminUsers.Any(item => item.Id != user.Id && string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<TenantAdminUserDto>.Fail("Admin user email is already registered for this tenant.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        user.Name = TenantAdminUserSupport.NormalizeName(request.Request.Name, email);
        user.Email = email;
        user.UpdatedAt = now;
        if (initialAdminAdded)
        {
            tenant.ActiveUserCount = TenantAdminUserSupport.CountUsersQuotaUsage(tenant);
        }

        TenantAdminUserSupport.AddActivity(tenant, "tenant.admin_user.updated", $"Admin user '{email}' updated.", _currentUser.ActorName, now);
        await _repository.UpdateAsync(tenant, cancellationToken);

        return Response<TenantAdminUserDto>.Success(TenantAdminUserSupport.ToDto(user));
    }
}
