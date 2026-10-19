using AutoMapper;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, Response<TenantDetailDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMapper _mapper;
    private readonly IPlatformActorTenantScope _scope;

    public UpdateTenantCommandHandler(
        ITenantRegistryRepository repository,
        ICurrentUserContext currentUser,
        IMapper mapper,
        IPlatformActorTenantScope scope)
    {
        _repository = repository;
        _currentUser = currentUser;
        _mapper = mapper;
        _scope = scope;
    }

    public async Task<Response<TenantDetailDto>> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _repository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return Response<TenantDetailDto>.Fail("Tenant not found.", 404);
        }

        // BL-521 FIX1 F3 — the tenant TYPE is a platform administrator's decision. Internal tenants cannot be suspended
        // or deleted, so a partner that could set Internal would make its own tenant untouchable. Sending the type the
        // tenant already has is not a change (and is what the edit form does).
        var tenantType = request.Request.TenantType ?? TenantType.Customer;
        if (tenantType != tenant.TenantType && !(await _scope.GetAsync(cancellationToken)).IsUnrestricted)
        {
            return Response<TenantDetailDto>.Fail(TenantTypeChangeRefused, 403);
        }

        var normalizedDomain = NormalizeDomain(request.Request.Domain, request.Request.Subdomain);
        var normalizedSlug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Request.Slug)
            ? request.Request.DisplayName
            : request.Request.Slug);

        if (!string.Equals(tenant.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase))
        {
            var existingSlug = await _repository.GetBySlugAsync(normalizedSlug, cancellationToken);
            if (existingSlug != null && existingSlug.Id != tenant.Id)
            {
                return Response<TenantDetailDto>.Fail("Slug is already in use.", 409);
            }
        }

        if (!string.Equals(tenant.Domain, normalizedDomain, StringComparison.OrdinalIgnoreCase))
        {
            var existingDomain = await _repository.GetByDomainAsync(normalizedDomain, cancellationToken);
            if (existingDomain != null && existingDomain.Id != tenant.Id)
            {
                return Response<TenantDetailDto>.Fail("Domain is already in use.", 409);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.ActorName;

        tenant.Name = request.Request.Name.Trim();
        tenant.DisplayName = request.Request.DisplayName.Trim();
        tenant.Slug = normalizedSlug;
        tenant.Domain = normalizedDomain;
        tenant.TenantType = tenantType;
        tenant.Country = string.IsNullOrWhiteSpace(request.Request.Country)
            ? null
            : request.Request.Country.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(request.Request.DefaultTimezone))
        {
            tenant.DefaultTimezone = request.Request.DefaultTimezone.Trim();
            tenant.Settings.Timezone = tenant.DefaultTimezone;
        }

        if (!string.IsNullOrWhiteSpace(request.Request.DefaultLanguage))
        {
            tenant.DefaultLanguage = request.Request.DefaultLanguage.Trim();
            tenant.Settings.Language = tenant.DefaultLanguage;
        }

        if (!string.IsNullOrWhiteSpace(request.Request.DefaultCurrency))
        {
            tenant.DefaultCurrency = request.Request.DefaultCurrency.Trim().ToUpperInvariant();
            tenant.Settings.Currency = tenant.DefaultCurrency;
        }

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = "tenant.profile.updated",
            Message = "Tenant profile updated.",
            At = now,
            Actor = actor
        });

        await _repository.UpdateAsync(tenant, cancellationToken);

        return Response<TenantDetailDto>.Success(_mapper.Map<TenantDetailDto>(tenant));
    }

    public const string TenantTypeChangeRefused = "Only a platform administrator can change the tenant type.";

    private static string NormalizeSlug(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var chars = lower.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        return string.Join('-', new string(chars)
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeDomain(string domain, string? subdomain)
    {
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var normalizedSubdomain = string.IsNullOrWhiteSpace(subdomain)
            ? null
            : NormalizeSlug(subdomain);

        if (string.IsNullOrWhiteSpace(normalizedSubdomain) ||
            normalizedDomain.StartsWith($"{normalizedSubdomain}.", StringComparison.Ordinal))
        {
            return normalizedDomain;
        }

        return $"{normalizedSubdomain}.{normalizedDomain}";
    }
}
