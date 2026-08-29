using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Users.Handlers.QueryHandlers;

public sealed class ValidateUserReferenceQueryHandler
    : IRequestHandler<ValidateUserReferenceQuery, Response<TenantUserLookupValidationDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public ValidateUserReferenceQueryHandler(IUserRepository userRepository, ITenantContext tenantContext)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TenantUserLookupValidationDto>> Handle(
        ValidateUserReferenceQuery request,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
        {
            return Response<TenantUserLookupValidationDto>.Fail("UserId is required.", 400);
        }

        if (!_tenantContext.IsResolved)
        {
            return Response<TenantUserLookupValidationDto>.Fail("Tenant context is required.", 400);
        }

        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null || !user.IsActive)
        {
            return Response<TenantUserLookupValidationDto>.Fail("User not found.", 404);
        }

        return Response<TenantUserLookupValidationDto>.Success(
            new TenantUserLookupValidationDto(
                user.Id,
                Referenceable: true,
                MaskedName: MaskedIdentity.MaskName(user.FirstName, user.LastName),
                MaskedEmail: MaskedIdentity.MaskEmail(user.Email)));
    }
}
