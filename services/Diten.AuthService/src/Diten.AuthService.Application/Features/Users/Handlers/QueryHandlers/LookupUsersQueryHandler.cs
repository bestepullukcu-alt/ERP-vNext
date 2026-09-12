using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — shapes the repository's active-user search into <see cref="UserLookupItemDto"/>
/// rows. The active/non-deleted/tenant filters live in <c>IUserRepository.SearchActiveAsync</c> (one place, exercised
/// against real Mongo by AccountKindEndpointTests); this handler adds the tenant from the resolved context and the
/// label, and nothing else — no email, no roles, no kind.
/// </summary>
public sealed class LookupUsersQueryHandler
    : IRequestHandler<LookupUsersQuery, Response<IReadOnlyList<UserLookupItemDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public LookupUsersQueryHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<IReadOnlyList<UserLookupItemDto>>> Handle(LookupUsersQuery request, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved)
        {
            return Response<IReadOnlyList<UserLookupItemDto>>.Fail("Tenant context is required.", 400);
        }

        var users = await _userRepository.SearchActiveAsync(
            _tenantContext.TenantId,
            request.Search?.Trim(),
            request.Limit,
            ct);

        IReadOnlyList<UserLookupItemDto> items = users
            .Select(u => new UserLookupItemDto(u.Id, DisplayLabel(u.FirstName, u.LastName)))
            .ToList();

        return Response<IReadOnlyList<UserLookupItemDto>>.Success(items);
    }

    /// <summary>"First Last", collapsing a missing half; never the email (a label must not disclose it).</summary>
    public static string DisplayLabel(string? firstName, string? lastName)
        => string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }.Where(p => !string.IsNullOrEmpty(p)));
}
