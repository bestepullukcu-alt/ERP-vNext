using AutoMapper;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class UpdateTenantBrandingCommandHandler : IRequestHandler<UpdateTenantBrandingCommand, TenantDetailDto?>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMapper _mapper;

    public UpdateTenantBrandingCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IMapper mapper)
    {
        _repository = repository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<TenantDetailDto?> Handle(UpdateTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.ActorName;

        tenant.LogoDataUrl = NormalizeDataUrl(request.Request.LogoDataUrl);
        tenant.FaviconDataUrl = NormalizeDataUrl(request.Request.FaviconDataUrl);
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = "tenant.branding.updated",
            Message = "Tenant logo and favicon updated.",
            At = now,
            Actor = actor
        });

        await _repository.UpdateAsync(tenant, cancellationToken);

        return _mapper.Map<TenantDetailDto>(tenant);
    }

    private static string? NormalizeDataUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
