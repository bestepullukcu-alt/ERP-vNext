using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class DeleteTenantAdminUserCommandHandler : IRequestHandler<DeleteTenantAdminUserCommand, Response<NoContent>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public DeleteTenantAdminUserCommandHandler(ITenantRegistryRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DeleteTenantAdminUserCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<NoContent>.Fail("Tenant not found.", 404);
        }

        TenantAdminUserSupport.EnsureInitialAdminUser(tenant);
        var user = tenant.AdminUsers.FirstOrDefault(item => item.Id == request.AdminUserId);
        if (user == null)
        {
            return Response<NoContent>.Fail("Admin user not found.", 404);
        }

        tenant.AdminUsers.Remove(user);
        TenantAdminUserSupport.AddActivity(tenant, "tenant.admin_user.deleted", $"Admin user '{user.Email}' deleted.", _currentUser.ActorName, DateTimeOffset.UtcNow);
        await _repository.UpdateAsync(tenant, cancellationToken);
        return Response<NoContent>.Success(204);
    }
}
