using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

/// <summary>
/// WP-INFRA-AUTH-DISPLAY-LABEL-01 — tenant-scoped read of the display label (skeleton: <see cref="GetAccountAssertionQueryHandler"/>).
///
/// <para>ONE 404 for two situations, same as the assertion endpoint: "no such user" and "that user belongs to
/// another tenant" both return the identical <c>Response.Fail(GetAccountAssertionQueryHandler.NotFoundMessage, 404)</c>
/// so a caller cannot use this endpoint to learn that an id exists elsewhere. UserName and Email are never read here:
/// the label is built exclusively from FirstName/LastName (see <see cref="LookupUsersQueryHandler.DisplayLabel"/>),
/// and it is neither truncated nor length-limited — a consumer applies its own display policy.</para>
/// </summary>
public sealed class GetUserDisplayLabelQueryHandler
    : IRequestHandler<GetUserDisplayLabelQuery, Response<UserDisplayLabelDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public GetUserDisplayLabelQueryHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<UserDisplayLabelDto>> Handle(GetUserDisplayLabelQuery request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
        {
            return Response<UserDisplayLabelDto>.Fail("UserId is required.", 400);
        }

        if (!_tenantContext.IsResolved)
        {
            return Response<UserDisplayLabelDto>.Fail("Tenant context is required.", 400);
        }

        // The repository applies TenantId + IsDeleted; a foreign tenant's user is simply "not found" here.
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null)
        {
            return Response<UserDisplayLabelDto>.Fail(GetAccountAssertionQueryHandler.NotFoundMessage, 404);
        }

        var label = LookupUsersQueryHandler.DisplayLabel(user.FirstName, user.LastName);
        return string.IsNullOrEmpty(label)
            ? Response<UserDisplayLabelDto>.Success(new UserDisplayLabelDto(user.Id, null, UserDisplayLabelStates.Unnamed))
            : Response<UserDisplayLabelDto>.Success(new UserDisplayLabelDto(user.Id, label, UserDisplayLabelStates.Named));
    }
}
