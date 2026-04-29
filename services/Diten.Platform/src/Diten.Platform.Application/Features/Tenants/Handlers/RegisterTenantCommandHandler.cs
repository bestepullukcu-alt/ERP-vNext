using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Events;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, Guid>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ITenantDomainRepository _domainRepository;
    private readonly ITenantLoginSettingsRepository _loginSettingsRepository;
    private readonly ITenantDefaultsProvider _defaults;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<RegisterTenantCommandHandler> _logger;

    public RegisterTenantCommandHandler(
        ITenantRegistryRepository repository,
        ITenantDomainRepository domainRepository,
        ITenantLoginSettingsRepository loginSettingsRepository,
        ITenantDefaultsProvider defaults,
        ICurrentUserContext currentUser,
        ILogger<RegisterTenantCommandHandler> logger)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _loginSettingsRepository = loginSettingsRepository;
        _defaults = defaults;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Guid> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        // 1. Build slug
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? BuildSlug(request.Name)
            : request.Slug.Trim().ToLowerInvariant();

        // 2. Slug uniqueness check
        var existingSlug = await _repository.GetBySlugAsync(slug, cancellationToken);
        if (existingSlug != null)
        {
            throw new InvalidOperationException($"Tenant slug '{slug}' already exists.");
        }

        // 3. Domain normalize & uniqueness check
        var normalizedDomain = NormalizeDomain(request.Domain, request.Subdomain);
        var existingDomain = await _repository.GetByDomainAsync(normalizedDomain, cancellationToken);
        if (existingDomain != null)
        {
            throw new InvalidOperationException($"Tenant domain '{normalizedDomain}' already exists.");
        }

        // 4. Build primary platform subdomain
        var platformDomain = $"{slug}.ditenteknoloji.com";

        // Check platform domain uniqueness (may differ from normalizedDomain)
        if (!string.Equals(platformDomain, normalizedDomain, StringComparison.OrdinalIgnoreCase))
        {
            var existingPlatformDomain = await _domainRepository.GetByDomainNameAsync(platformDomain, cancellationToken);
            if (existingPlatformDomain != null)
            {
                throw new InvalidOperationException($"Platform domain '{platformDomain}' already exists.");
            }
        }

        // 5. Generate unique code
        var code = await GenerateUniqueCodeAsync(request.Name, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId.ToString()
            : "system";

        // 6. Build tenant entity
        var tenant = new Tenant
        {
            Code = code,
            Slug = slug,
            Name = request.Name.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name.Trim() : request.DisplayName.Trim(),
            Domain = platformDomain,
            Region = string.IsNullOrWhiteSpace(request.Region) ? _defaults.DefaultRegion : request.Region.Trim().ToUpperInvariant(),
            Environment = string.IsNullOrWhiteSpace(request.Environment) ? _defaults.DefaultEnvironment : request.Environment.Trim(),
            Status = TenantStatus.Provisioning,
            Tier = string.IsNullOrWhiteSpace(request.Tier) ? _defaults.DefaultTier : request.Tier.Trim(),
            TenantType = request.TenantType ?? TenantType.Trial,

            // Legal & Company
            LegalName = request.LegalName?.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            Country = request.Country?.Trim().ToUpperInvariant(),
            Industry = request.Industry?.Trim(),

            // Contact
            ContactPerson = request.ContactPerson?.Trim(),
            ContactEmail = request.ContactEmail?.Trim().ToLowerInvariant(),
            ContactPhone = request.ContactPhone?.Trim(),

            // Locale defaults
            DefaultTimezone = string.IsNullOrWhiteSpace(request.DefaultTimezone) ? _defaults.DefaultTimezone : request.DefaultTimezone.Trim(),
            DefaultLanguage = string.IsNullOrWhiteSpace(request.DefaultLanguage) ? _defaults.DefaultLanguage : request.DefaultLanguage.Trim(),
            DefaultCurrency = string.IsNullOrWhiteSpace(request.DefaultCurrency) ? _defaults.DefaultCurrency : request.DefaultCurrency.Trim().ToUpperInvariant(),

            ProvisioningStatus = "Started",
            AppUrl = _defaults.AppUrlTemplate.Replace("{tenant}", slug, StringComparison.OrdinalIgnoreCase),
            Settings = new TenantSettings
            {
                Language = string.IsNullOrWhiteSpace(request.DefaultLanguage) ? _defaults.DefaultLanguage : request.DefaultLanguage.Trim(),
                Timezone = string.IsNullOrWhiteSpace(request.DefaultTimezone) ? _defaults.DefaultTimezone : request.DefaultTimezone.Trim(),
                Currency = string.IsNullOrWhiteSpace(request.DefaultCurrency) ? _defaults.DefaultCurrency : request.DefaultCurrency.Trim().ToUpperInvariant(),
                Environment = string.IsNullOrWhiteSpace(request.Environment) ? _defaults.DefaultEnvironment : request.Environment.Trim()
            },
            ProvisioningSteps =
            [
                new TenantProvisioningStep
                {
                    Key = "registry-created",
                    Label = "Registry Record Created",
                    Status = "Completed",
                    CreatedAt = now,
                    CompletedAt = now,
                    Detail = "Tenant registry entry was created."
                },
                new TenantProvisioningStep
                {
                    Key = "domain-assigned",
                    Label = "Platform Domain Assigned",
                    Status = "Completed",
                    CreatedAt = now,
                    CompletedAt = now,
                    Detail = $"Primary platform domain '{platformDomain}' assigned."
                },
                new TenantProvisioningStep
                {
                    Key = "bootstrap-platform",
                    Label = "Platform Bootstrap",
                    Status = "InProgress",
                    CreatedAt = now,
                    Detail = "Platform bootstrap pipeline started."
                }
            ],
            ActivityTimeline =
            [
                new TenantActivityEvent
                {
                    EventType = "tenant.created",
                    Message = "Tenant created.",
                    Actor = actor,
                    At = now
                },
                new TenantActivityEvent
                {
                    EventType = "tenant.provisioning.started",
                    Message = "Provisioning started.",
                    Actor = actor,
                    At = now
                }
            ],
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor
        };

        // 7. Persist tenant
        await _repository.CreateAsync(tenant, cancellationToken);

        // 8. Create default platform TenantDomain record
        var tenantDomain = new TenantDomain
        {
            TenantId = tenant.Id,
            DomainName = platformDomain,
            Type = DomainType.Platform,
            IsPrimary = true,
            IsLoginDomain = true,
            IsVerified = true,
            Status = TenantDomainStatus.Active,
            VerifiedAt = now,
            CreatedBy = actor,
            UpdatedAt = now,
            UpdatedBy = actor
        };

        await _domainRepository.CreateAsync(tenantDomain, cancellationToken);

        var loginSettings = TenantLoginSettingsMapper.CreateDefault(tenant.Id, actor, now);
        await _loginSettingsRepository.CreateAsync(loginSettings, cancellationToken);

        _logger.LogInformation(
            "Tenant '{TenantCode}' registered with slug '{Slug}' and primary domain '{Domain}'",
            code, slug, platformDomain);

        // 9. Prepare integration event contracts (dispatch NOT implemented — placeholder for future outbox)
        if (request.InitialAdmin != null)
        {
            // In a future task, this will be dispatched via the outbox pattern:
            // var invitationEvent = new TenantAdminInvitationRequestedIntegrationEvent(...)
            // await _outbox.EnqueueAsync(invitationEvent, cancellationToken);

            tenant.ActivityTimeline.Add(new TenantActivityEvent
            {
                EventType = "tenant.admin.invitation.queued",
                Message = $"Initial admin invitation queued for {request.InitialAdmin.Email}.",
                Actor = actor,
                At = now
            });

            tenant.ProvisioningSteps.Add(new TenantProvisioningStep
            {
                Key = "admin-invitation",
                Label = "Initial Admin Invitation",
                Status = "Pending",
                CreatedAt = now,
                Detail = $"Invitation for {request.InitialAdmin.FirstName} {request.InitialAdmin.LastName} ({request.InitialAdmin.Email}) pending AuthService integration."
            });

            await _repository.UpdateAsync(tenant, cancellationToken);

            _logger.LogInformation(
                "Initial admin invitation queued for tenant '{TenantCode}': {Email}",
                code, request.InitialAdmin.Email);
        }

        return tenant.Id;
    }

    private async Task<string> GenerateUniqueCodeAsync(string name, CancellationToken cancellationToken)
    {
        var stem = BuildCodeStem(name);

        for (var i = 0; i < 20; i++)
        {
            var suffix = Random.Shared.Next(1000, 9999);
            var candidate = $"{stem}{suffix}";
            var existing = await _repository.GetByCodeAsync(candidate, cancellationToken);
            if (existing == null)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to generate unique tenant code.");
    }

    private static string NormalizeDomain(string domain, string? subdomain)
    {
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var normalizedSubdomain = string.IsNullOrWhiteSpace(subdomain)
            ? string.Empty
            : subdomain.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedSubdomain) ||
            normalizedDomain.StartsWith($"{normalizedSubdomain}.", StringComparison.Ordinal))
        {
            return normalizedDomain;
        }

        return $"{normalizedSubdomain}.{normalizedDomain}";
    }

    private static string BuildCodeStem(string name)
    {
        var letters = new string(name
            .Where(char.IsLetterOrDigit)
            .Take(6)
            .ToArray())
            .ToUpperInvariant();

        return string.IsNullOrWhiteSpace(letters) ? "TENANT" : letters.PadRight(6, 'X');
    }

    private static string BuildSlug(string value)
    {
        return string.Join('-', value
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
