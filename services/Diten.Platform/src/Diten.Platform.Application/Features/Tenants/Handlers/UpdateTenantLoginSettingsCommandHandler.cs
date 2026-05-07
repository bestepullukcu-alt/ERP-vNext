using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class UpdateTenantLoginSettingsCommandHandler : IRequestHandler<UpdateTenantLoginSettingsCommand, TenantLoginSettingsDto?>
{
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ITenantLoginSettingsRepository _settingsRepository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTenantLoginSettingsCommandHandler(
        ITenantRegistryRepository tenantRepository,
        ITenantLoginSettingsRepository settingsRepository,
        ICurrentUserContext currentUser)
    {
        _tenantRepository = tenantRepository;
        _settingsRepository = settingsRepository;
        _currentUser = currentUser;
    }

    public async Task<TenantLoginSettingsDto?> Handle(UpdateTenantLoginSettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var actor = GetActor();
        var settings = await _settingsRepository.GetByTenantRefIdAsync(request.TenantId, cancellationToken)
            ?? TenantLoginSettingsMapper.CreateDefault(request.TenantId, actor, now);

        Apply(settings, request.Request, actor, now);

        if (settings.Id == Guid.Empty)
        {
            await _settingsRepository.CreateAsync(settings, cancellationToken);
        }
        else
        {
            var existing = await _settingsRepository.GetByTenantRefIdAsync(request.TenantId, cancellationToken);
            if (existing == null)
            {
                await _settingsRepository.CreateAsync(settings, cancellationToken);
            }
            else
            {
                await _settingsRepository.UpdateAsync(settings, cancellationToken);
            }
        }

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actor;
        tenant.ActivityTimeline.Add(new TenantActivityEvent
        {
            EventType = "tenant.login_settings.updated",
            Message = "Tenant login and security settings updated.",
            At = now,
            Actor = actor
        });
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        return TenantLoginSettingsMapper.ToDto(settings);
    }

    private static void Apply(
        TenantLoginSettings settings,
        TenantLoginSettingsUpdateRequest request,
        string actor,
        DateTimeOffset now)
    {
        settings.TwoFactorEnabled = request.TwoFactorEnabled;
        settings.MfaRequired = request.MfaRequired;
        settings.EmailLoginEnabled = request.EmailLoginEnabled;
        settings.PhoneLoginEnabled = request.PhoneLoginEnabled;
        settings.PasswordMinLength = request.PasswordMinLength;
        settings.PasswordRequireUppercase = request.PasswordRequireUppercase;
        settings.PasswordRequireLowercase = request.PasswordRequireLowercase;
        settings.PasswordRequireDigit = request.PasswordRequireDigit;
        settings.PasswordRequireSpecialChar = request.PasswordRequireSpecialChar;
        settings.PasswordExpirationDays = request.PasswordExpirationDays;
        settings.SessionTimeoutMinutes = request.SessionTimeoutMinutes;
        settings.RefreshTokenLifetimeDays = request.RefreshTokenLifetimeDays;
        settings.MaxFailedLoginAttempts = request.MaxFailedLoginAttempts;
        settings.LockoutDurationMinutes = request.LockoutDurationMinutes;
        settings.IpWhitelistEnabled = request.IpWhitelistEnabled;
        settings.AllowedIps = TenantLoginSettingsMapper.NormalizeList(request.AllowedIps).ToList();
        settings.AllowedCountries = TenantLoginSettingsMapper.NormalizeList(request.AllowedCountries, toUpper: true).ToList();
        settings.LoginAuditRetentionDays = request.LoginAuditRetentionDays;
        settings.UpdatedAt = now;
        settings.UpdatedBy = actor;
    }

    private string GetActor()
    {
        return _currentUser.ActorName;
    }
}
