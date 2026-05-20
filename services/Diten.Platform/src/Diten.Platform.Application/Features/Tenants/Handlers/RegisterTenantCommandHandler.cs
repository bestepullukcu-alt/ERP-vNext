using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, Response<Guid>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ITenantDomainRepository _domainRepository;
    private readonly ITenantLoginSettingsRepository _loginSettingsRepository;
    private readonly ITenantDefaultsProvider _defaults;
    private readonly ICurrentUserContext _currentUser;
    private readonly IEventBus _eventBus;
    private readonly ILogger<RegisterTenantCommandHandler> _logger;

    public RegisterTenantCommandHandler(
        ITenantRegistryRepository repository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ITenantDomainRepository domainRepository,
        ITenantLoginSettingsRepository loginSettingsRepository,
        ITenantDefaultsProvider defaults,
        ICurrentUserContext currentUser,
        IEventBus eventBus,
        ILogger<RegisterTenantCommandHandler> logger)
    {
        _repository = repository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _domainRepository = domainRepository;
        _loginSettingsRepository = loginSettingsRepository;
        _defaults = defaults;
        _currentUser = currentUser;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Response<Guid>> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        // 1. Build slug
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? BuildSlug(request.Name)
            : request.Slug.Trim().ToLowerInvariant();

        // 2. Slug uniqueness check
        var existingSlug = await _repository.GetBySlugAsync(slug, cancellationToken);
        if (existingSlug != null)
        {
            return Response<Guid>.Fail($"Tenant slug '{slug}' already exists.", 409);
        }

        // 3. Domain normalize & uniqueness check
        var normalizedDomain = NormalizeDomain(request.Domain, request.Subdomain);
        var existingDomain = await _repository.GetByDomainAsync(normalizedDomain, cancellationToken);
        if (existingDomain != null)
        {
            return Response<Guid>.Fail($"Tenant domain '{normalizedDomain}' already exists.", 409);
        }

        // 4. Build primary platform subdomain
        var platformDomain = $"{slug}.ditenteknoloji.com";

        // Check platform domain uniqueness against TenantDomain records as well.
        var existingPlatformDomain = await _domainRepository.GetByDomainNameAsync(platformDomain, cancellationToken);
        if (existingPlatformDomain != null)
        {
            return Response<Guid>.Fail($"Platform domain '{platformDomain}' already exists.", 409);
        }

        // 5. Generate unique code
        var code = await GenerateUniqueCodeAsync(request.Name, cancellationToken);
        if (code is null)
        {
            return Response<Guid>.Fail("Unable to generate unique tenant code.", 500);
        }
        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.ActorName;
        var tier = string.IsNullOrWhiteSpace(request.Tier) ? _defaults.DefaultTier : request.Tier.Trim();
        var tenantLimits = ResolveTenantLimits(tier);
        if (!request.PlanId.HasValue || request.PlanId.Value == Guid.Empty)
        {
            return Response<Guid>.Fail("PlanId is required.", 400);
        }

        var selectedPlan = await _subscriptionPlanRepository.GetByIdAsync(request.PlanId.Value, cancellationToken);
        if (selectedPlan == null)
        {
            return Response<Guid>.Fail("Selected subscription plan was not found.", 404);
        }

        if (!selectedPlan.IsActive)
        {
            return Response<Guid>.Fail("Selected subscription plan is not active.", 400);
        }

        if (selectedPlan.IsTrialPlan && (!selectedPlan.TrialDurationDays.HasValue || selectedPlan.TrialDurationDays.Value <= 0))
        {
            return Response<Guid>.Fail("Selected trial plan has an invalid trial duration.", 400);
        }

        var subscriptionStatus = selectedPlan.IsTrialPlan
            ? Diten.Platform.Domain.Enums.TenantSubscriptionStatus.Trialing
            : Diten.Platform.Domain.Enums.TenantSubscriptionStatus.Active;
        var trialStartDateUtc = selectedPlan.IsTrialPlan ? now : (DateTimeOffset?)null;
        var trialEndDateUtc = selectedPlan.IsTrialPlan ? now.AddDays(selectedPlan.TrialDurationDays!.Value) : (DateTimeOffset?)null;
        var currentPeriodStartUtc = selectedPlan.IsTrialPlan ? (DateTimeOffset?)null : now;
        var currentPeriodEndUtc = selectedPlan.IsTrialPlan ? (DateTimeOffset?)null : now.AddMonths(1);

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
            Tier = tier,
            TenantType = request.TenantType ?? TenantType.Customer,
            PlanId = selectedPlan.Id,
            PlanCode = selectedPlan.Code,
            PlanName = selectedPlan.Name,
            SubscriptionStatus = subscriptionStatus,
            TrialStartDateUtc = trialStartDateUtc,
            TrialEndDateUtc = trialEndDateUtc,
            ActiveUserCount = 0,
            UserLimit = tenantLimits.UserLimit,
            StorageUsedGb = 0,
            StorageQuotaGb = tenantLimits.StorageQuotaGb,

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
            AdminUsers = request.InitialAdmin == null
                ? []
                :
                [
                    new TenantAdminUser
                    {
                        Name = string.IsNullOrWhiteSpace(string.Join(' ', new[] { request.InitialAdmin.FirstName, request.InitialAdmin.LastName }
                            .Where(part => !string.IsNullOrWhiteSpace(part))).Trim())
                            ? request.InitialAdmin.Email.Trim().ToLowerInvariant()
                            : string.Join(' ', new[] { request.InitialAdmin.FirstName, request.InitialAdmin.LastName }
                                .Where(part => !string.IsNullOrWhiteSpace(part))).Trim(),
                        Email = request.InitialAdmin.Email.Trim().ToLowerInvariant(),
                        Status = TenantAdminUserStatus.Invited,
                        InvitedAt = now,
                        UpdatedAt = now
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

        var subscriptionResult = await TenantSubscriptionCommandSupport.CreateAsync(
            tenant.Id,
            selectedPlan.Id,
            selectedPlan.IsTrialPlan,
            trialEndDateUtc,
            currentPeriodStartUtc,
            currentPeriodEndUtc,
            "tenant-registration",
            _repository,
            _subscriptionPlanRepository,
            _tenantSubscriptionRepository,
            _currentUser,
            cancellationToken);

        if (!subscriptionResult.IsSuccessful)
        {
            return Response<Guid>.Fail(subscriptionResult.Errors, subscriptionResult.StatusCode);
        }

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

        var initialAdminUserId = tenant.AdminUsers.FirstOrDefault()?.Id;
        Guid? actorUserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId;
        await _eventBus.PublishAsync(
            new TenantCreatedV1(
                tenant.Id,
                now,
                tenant.PlanId,
                actorUserId,
                tenant.DisplayName,
                tenant.DefaultLanguage,
                initialAdminUserId),
            new EventPublishOptions
            {
                TenantId = tenant.Id,
                Producer = "Diten.Platform",
                OccurredAtUtc = now
            },
            cancellationToken);

        return Response<Guid>.Success(tenant.Id, 201);
    }

    private async Task<string?> GenerateUniqueCodeAsync(string name, CancellationToken cancellationToken)
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

        return null;
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

    private static (int UserLimit, decimal StorageQuotaGb) ResolveTenantLimits(string? tier)
    {
        return (tier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "enterprise" => (500, 500),
            "premium" => (250, 250),
            "standard" => (50, 100),
            "trial" => (10, 10),
            _ => (50, 100)
        };
    }

    private static string BuildSlug(string value)
    {
        return string.Join('-', value
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
