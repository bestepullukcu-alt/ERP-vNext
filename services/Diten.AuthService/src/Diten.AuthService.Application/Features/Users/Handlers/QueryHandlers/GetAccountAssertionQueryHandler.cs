using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — tenant-scoped read of the account fact (pattern: ValidateUserReferenceQueryHandler).
///
/// <para>ONE 404 for two situations. "No such user" and "that user belongs to another tenant" return the identical
/// <c>Response.Fail("User not found.", 404)</c> — same message, same code, same body bytes — so a caller cannot use
/// this endpoint to learn that an id exists elsewhere. Unlike lookup-validation, an INACTIVE user still answers 200
/// with <c>active=false</c>: inactivity is a fact the consumer wants, not a reason to hide the account.</para>
/// </summary>
public sealed class GetAccountAssertionQueryHandler
    : IRequestHandler<GetAccountAssertionQuery, Response<AccountAssertionDto>>
{
    public const string NotFoundMessage = "User not found.";

    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public GetAccountAssertionQueryHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AccountAssertionDto>> Handle(GetAccountAssertionQuery request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
        {
            return Response<AccountAssertionDto>.Fail("UserId is required.", 400);
        }

        if (!_tenantContext.IsResolved)
        {
            return Response<AccountAssertionDto>.Fail("Tenant context is required.", 400);
        }

        // The repository applies TenantId + IsDeleted; a foreign tenant's user is simply "not found" here.
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null)
        {
            return Response<AccountAssertionDto>.Fail(NotFoundMessage, 404);
        }

        return Response<AccountAssertionDto>.Success(new AccountAssertionDto(
            user.Id,
            user.IsActive,
            user.AccountKind.ToString(),
            DateTimeOffset.UtcNow,
            user.UpdatedAt));
    }
}
