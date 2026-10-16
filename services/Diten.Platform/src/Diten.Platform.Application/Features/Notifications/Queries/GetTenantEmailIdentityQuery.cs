using Diten.Platform.Application.Features.Notifications.Services;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Queries;

/// <summary>
/// BL-454 — what AuthService needs to send ITS e-mails (the user invitation) under the same sender name, in the
/// same language and with the same reply address as Platform's. Four presentation values and nothing else: no
/// sender address, no host, no port, no credential reference, no provider, no policy.
/// </summary>
public sealed record TenantEmailIdentityDto(
    Guid TenantId,
    string DisplayName,
    string Language,
    string? SenderName,
    string? ReplyToEmail);

/// <summary>Null when the tenant does not exist.</summary>
public sealed record GetTenantEmailIdentityQuery(Guid TenantId) : IRequest<TenantEmailIdentityDto?>;

public sealed class GetTenantEmailIdentityQueryHandler : IRequestHandler<GetTenantEmailIdentityQuery, TenantEmailIdentityDto?>
{
    private readonly ITenantEmailIdentityResolver _resolver;

    public GetTenantEmailIdentityQueryHandler(ITenantEmailIdentityResolver resolver) => _resolver = resolver;

    public async Task<TenantEmailIdentityDto?> Handle(GetTenantEmailIdentityQuery request, CancellationToken ct)
    {
        // Guid.Empty is the platform, not a tenant: this endpoint answers for tenants only.
        if (request.TenantId == Guid.Empty)
        {
            return null;
        }

        var identity = await _resolver.ResolveAsync(request.TenantId, ct);
        return identity?.DisplayName is null
            ? null
            : new TenantEmailIdentityDto(
                request.TenantId, identity.DisplayName, identity.Language, identity.SenderName, identity.ReplyToEmail);
    }
}
