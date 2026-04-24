using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, Guid>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly ITenantDefaultsProvider _defaults;
    private readonly ICurrentUserContext _currentUser;

    public RegisterTenantCommandHandler(
        ITenantRegistryRepository repository,
        ITenantDefaultsProvider defaults,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _defaults = defaults;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var normalizedDomain = NormalizeDomain(request.Domain, request.Subdomain);
        var existingDomain = await _repository.GetByDomainAsync(normalizedDomain, cancellationToken);
        if (existingDomain != null)
        {
            throw new InvalidOperationException($"Tenant domain '{normalizedDomain}' already exists.");
        }

        var code = await GenerateUniqueCodeAsync(request.Name, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var actor = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId.ToString()
            : "system";

        var tenant = new Tenant
        {
            Code = code,
            Slug = BuildSlug(request.Name),
            Name = request.Name.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name.Trim() : request.DisplayName.Trim(),
            Domain = normalizedDomain,
            Region = string.IsNullOrWhiteSpace(request.Region) ? _defaults.DefaultRegion : request.Region.Trim().ToUpperInvariant(),
            Environment = string.IsNullOrWhiteSpace(request.Environment) ? _defaults.DefaultEnvironment : request.Environment.Trim(),
            Status = TenantStatus.Provisioning,
            Tier = string.IsNullOrWhiteSpace(request.Tier) ? _defaults.DefaultTier : request.Tier.Trim(),
            ProvisioningStatus = "Started",
            AppUrl = _defaults.AppUrlTemplate.Replace("{tenant}", BuildSlug(request.Name), StringComparison.OrdinalIgnoreCase),
            Settings = new TenantSettings
            {
                Language = _defaults.DefaultLanguage,
                Timezone = _defaults.DefaultTimezone,
                Currency = _defaults.DefaultCurrency,
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

        await _repository.CreateAsync(tenant, cancellationToken);

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
