using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditApplicationCoreTests
{
    [Fact]
    public void SensitiveFieldRedactor_ShouldRedactNestedSensitiveFields()
    {
        var redactor = CreateRedactor();
        var payload = new Dictionary<string, object?>
        {
            ["profile"] = new Dictionary<string, object?>
            {
                ["credentials"] = new Dictionary<string, object?>
                {
                    ["password"] = "raw-password",
                    ["token"] = "raw-token",
                    ["displayName"] = "Visible"
                },
                ["safe"] = "keep-me"
            }
        };

        var redacted = redactor.RedactDictionary(payload);
        var profile = Child(redacted, "profile");
        var credentials = Child(profile, "credentials");

        Assert.Equal("[REDACTED]", credentials["password"]);
        Assert.Equal("[REDACTED]", credentials["token"]);
        Assert.Equal("Visible", credentials["displayName"]);
        Assert.Equal("keep-me", profile["safe"]);
    }

    [Fact]
    public void SensitiveFieldRedactor_ShouldMatchSensitiveFieldsCaseInsensitive()
    {
        var redactor = CreateRedactor();
        var payload = new Dictionary<string, object?>
        {
            ["PaSsWoRd"] = "raw-password",
            ["APIKEY"] = "raw-api-key",
            ["Authorization"] = "Bearer raw-token"
        };

        var redacted = redactor.RedactDictionary(payload);

        Assert.Equal("[REDACTED]", redacted["PaSsWoRd"]);
        Assert.Equal("[REDACTED]", redacted["APIKEY"]);
        Assert.Equal("[REDACTED]", redacted["Authorization"]);
    }

    [Fact]
    public async Task AuditService_ShouldRedactBeforeAfterAndMetadataBeforeOutboxWrite()
    {
        var tenantId = Guid.NewGuid();
        var writer = new CapturingAuditOutboxWriter();
        var service = CreateAuditService(writer, tenantId);

        var result = await service.AppendAsync(CreateAppendRequest() with
        {
            BeforeState = new Dictionary<string, object?> { ["password"] = "raw-before" },
            AfterState = new Dictionary<string, object?> { ["nested"] = new Dictionary<string, object?> { ["refreshToken"] = "raw-after" } },
            Metadata = new Dictionary<string, object?> { ["headers"] = new Dictionary<string, object?> { ["authorization"] = "Bearer raw-metadata" } }
        });

        Assert.Equal(AuditAppendStatus.Queued, result.Status);
        Assert.NotNull(writer.Request);
        var payload = writer.Request!.Payload;
        var beforeState = Child(payload, "BeforeState");
        var afterState = Child(Child(payload, "AfterState"), "nested");
        var metadataHeaders = Child(Child(payload, "Metadata"), "headers");

        Assert.Equal("[REDACTED]", beforeState["password"]);
        Assert.Equal("[REDACTED]", afterState["refreshToken"]);
        Assert.Equal("[REDACTED]", metadataHeaders["authorization"]);
        Assert.False(ContainsValue(payload, "raw-before"));
        Assert.False(ContainsValue(payload, "raw-after"));
        Assert.False(ContainsValue(payload, "raw-metadata"));
    }

    [Fact]
    public void AuditIdempotencyKeyBuilder_ShouldBeDeterministic()
    {
        var builder = new AuditIdempotencyKeyBuilder();
        var correlationId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var first = builder.Build(correlationId, "UpdateTenantCommand", "Tenant", entityId, AuditOperation.Update, sequence: 0);
        var second = builder.Build(correlationId, "UpdateTenantCommand", "Tenant", entityId, AuditOperation.Update, sequence: 0);
        var differentSequence = builder.Build(correlationId, "UpdateTenantCommand", "Tenant", entityId, AuditOperation.Update, sequence: 1);

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentSequence);
    }

    [Fact]
    public async Task AuditService_ShouldReturnControlledDuplicateResult()
    {
        var writer = new CapturingAuditOutboxWriter { EnqueueResult = false };
        var service = CreateAuditService(writer, Guid.NewGuid());

        var result = await service.AppendAsync(CreateAppendRequest());

        Assert.Equal(AuditAppendStatus.Duplicate, result.Status);
        Assert.True(result.IsDuplicate);
        Assert.False(result.ShouldBreakBusinessCommand);
        Assert.False(string.IsNullOrWhiteSpace(result.IdempotencyKey));
    }

    [Fact]
    public async Task RetentionResolver_ShouldFallbackToDefaultPolicy()
    {
        var defaultPolicy = CreateRetentionPolicy(AuditCategory.Security, AuditEventRetentionPolicy.DefaultPlanTierCode, minimum: 30, @default: 365, maximum: 730);
        var resolver = CreateRetentionResolver([defaultPolicy], []);

        var resolution = await resolver.ResolveAsync(new AuditRetentionResolutionRequest
        {
            Category = AuditCategory.Security,
            PlanTierCode = "Enterprise"
        });

        Assert.True(resolution.UsedDefaultPolicyFallback);
        Assert.Equal(AuditEventRetentionPolicy.DefaultPlanTierCode, resolution.EffectivePlanTierCode);
        Assert.Equal(365, resolution.EffectiveRetentionDays);
    }

    [Fact]
    public async Task RetentionResolver_ShouldClampTenantPreferenceToPolicyFloorAndCeiling()
    {
        var tenantId = Guid.NewGuid();
        var policy = CreateRetentionPolicy(AuditCategory.PlatformConfiguration, "Gold", minimum: 30, @default: 365, maximum: 730);
        var lowPreference = CreateTenantPreference(tenantId, AuditCategory.PlatformConfiguration, retentionDays: 7);
        var highPreference = CreateTenantPreference(tenantId, AuditCategory.PlatformConfiguration, retentionDays: 1000);
        var preferenceRepository = new InMemoryTenantAuditPreferenceRepository([lowPreference]);
        var resolver = new AuditRetentionPolicyResolver(new InMemoryAuditRetentionPolicyRepository([policy]), preferenceRepository);

        var floorResolution = await resolver.ResolveAsync(new AuditRetentionResolutionRequest
        {
            Category = AuditCategory.PlatformConfiguration,
            PlanTierCode = "Gold",
            TenantId = tenantId
        });

        preferenceRepository.SetPreferences([highPreference]);
        var ceilingResolution = await resolver.ResolveAsync(new AuditRetentionResolutionRequest
        {
            Category = AuditCategory.PlatformConfiguration,
            PlanTierCode = "Gold",
            TenantId = tenantId
        });

        Assert.True(floorResolution.TenantPreferenceApplied);
        Assert.Equal(30, floorResolution.EffectiveRetentionDays);
        Assert.True(ceilingResolution.TenantPreferenceApplied);
        Assert.Equal(730, ceilingResolution.EffectiveRetentionDays);
    }

    [Fact]
    public void AuditRecursionGuard_ShouldTrackActiveScope()
    {
        var guard = new AuditRecursionGuard();

        Assert.False(guard.IsActive);
        using (guard.BeginScope())
        {
            Assert.True(guard.IsActive);
            using (guard.BeginScope())
            {
                Assert.True(guard.IsActive);
            }

            Assert.True(guard.IsActive);
        }

        Assert.False(guard.IsActive);
    }

    [Fact]
    public async Task AuditService_ShouldEnqueueSuccessfulRequest()
    {
        var tenantId = Guid.NewGuid();
        var writer = new CapturingAuditOutboxWriter();
        var service = CreateAuditService(writer, tenantId);

        var result = await service.AppendAsync(CreateAppendRequest());

        Assert.Equal(AuditAppendStatus.Queued, result.Status);
        Assert.True(result.IsEnqueued);
        Assert.NotNull(writer.Request);
        Assert.Equal(tenantId, writer.Request!.TenantId);
        Assert.Equal(AuditOperation.Update, writer.Request.Operation);
        Assert.Equal("Tenant", writer.Request.EntityType);
    }

    [Fact]
    public async Task AuditService_ShouldReturnControlledResultWhenEnqueueFails()
    {
        var writer = new CapturingAuditOutboxWriter
        {
            ExceptionToThrow = new InvalidOperationException("simulated failure with password raw-secret")
        };
        var service = CreateAuditService(writer, Guid.NewGuid());

        var result = await service.AppendAsync(CreateAppendRequest());

        Assert.Equal(AuditAppendStatus.EnqueueFailed, result.Status);
        Assert.False(result.ShouldBreakBusinessCommand);
        Assert.DoesNotContain("raw-secret", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditService_ShouldRejectEmptyTenantIdAndUsePlatformSystemTenantForPlatformGlobalEvents()
    {
        var emptyTenantWriter = new CapturingAuditOutboxWriter();
        var emptyTenantService = CreateAuditService(emptyTenantWriter, Guid.Empty);

        var rejected = await emptyTenantService.AppendAsync(CreateAppendRequest());

        var platformWriter = new CapturingAuditOutboxWriter();
        var platformService = CreateAuditService(platformWriter, Guid.Empty);
        var platformResult = await platformService.AppendAsync(CreateAppendRequest() with { IsPlatformGlobal = true });

        Assert.Equal(AuditAppendStatus.Rejected, rejected.Status);
        Assert.False(rejected.ShouldBreakBusinessCommand);
        Assert.Equal(AuditAppendStatus.Queued, platformResult.Status);
        Assert.NotNull(platformWriter.Request);
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, platformWriter.Request!.TenantId);
    }

    [Fact]
    public void Rejected_ShouldNotBreakBusinessCommand_ByDefault()
    {
        var result = AuditAppendResult.Rejected("Audit correlation id is required.");

        Assert.Equal(AuditAppendStatus.Rejected, result.Status);
        Assert.False(result.ShouldBreakBusinessCommand);
        Assert.True(result.IsControlledFailure);
        Assert.Equal("Audit correlation id is required.", result.Diagnostic);
    }

    [Fact]
    public void RejectedCritical_ShouldBreakBusinessCommand()
    {
        var result = AuditAppendResult.RejectedCritical("Security-critical audit category requires successful append.");

        Assert.Equal(AuditAppendStatus.Rejected, result.Status);
        Assert.True(result.ShouldBreakBusinessCommand);
        Assert.True(result.IsControlledFailure);
        Assert.Equal("Security-critical audit category requires successful append.", result.Diagnostic);
    }

    private static SensitiveFieldRedactor CreateRedactor()
    {
        return new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry());
    }

    private static AuditService CreateAuditService(CapturingAuditOutboxWriter writer, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        return new AuditService(
            writer,
            CreateRedactor(),
            new AuditIdempotencyKeyBuilder(),
            new AuditRecursionGuard(),
            tenantContext,
            new TestCurrentUserContext(),
            NullLogger<AuditService>.Instance);
    }

    private static AuditRetentionPolicyResolver CreateRetentionResolver(
        IReadOnlyList<AuditEventRetentionPolicy> policies,
        IReadOnlyList<TenantAuditPreference> preferences)
    {
        return new AuditRetentionPolicyResolver(
            new InMemoryAuditRetentionPolicyRepository(policies),
            new InMemoryTenantAuditPreferenceRepository(preferences));
    }

    private static AuditAppendRequest CreateAppendRequest()
    {
        return new AuditAppendRequest
        {
            CorrelationId = Guid.Parse("f755c10d-a018-41f0-a250-f1e37909a76f"),
            RequestType = "UpdateTenantCommand",
            ActorType = AuditActorType.PlatformAdministrator,
            ActorId = Guid.Parse("b72ded0b-43c8-4e95-8158-bd56e391deaa"),
            ActorEmail = "admin@diten.com",
            ActorDisplayName = "Platform Admin",
            Category = AuditCategory.TenantAdministration,
            EntityType = "Tenant",
            EntityId = Guid.Parse("5c53a512-2248-4d68-8e96-900b44fec7e9"),
            Operation = AuditOperation.Update,
            Outcome = AuditOutcome.Succeeded,
            SourceModule = "Tenants"
        };
    }

    private static AuditEventRetentionPolicy CreateRetentionPolicy(
        AuditCategory category,
        string planTierCode,
        int minimum,
        int @default,
        int maximum)
    {
        return new AuditEventRetentionPolicy
        {
            Category = category,
            PlanTierCode = planTierCode,
            MinimumRetentionDays = minimum,
            DefaultRetentionDays = @default,
            MaximumRetentionDays = maximum,
            HotStorageDays = Math.Min(90, @default),
            ColdStoragePrepared = true,
            AllowTenantOverride = true,
            IsActive = true
        };
    }

    private static TenantAuditPreference CreateTenantPreference(Guid tenantId, AuditCategory category, int retentionDays)
    {
        return new TenantAuditPreference
        {
            TenantId = tenantId,
            Category = category,
            RetentionDays = retentionDays,
            UpdatedByActorId = Guid.NewGuid()
        };
    }

    private static IReadOnlyDictionary<string, object?> Child(IReadOnlyDictionary<string, object?> parent, string key)
    {
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(parent[key]);
    }

    private static bool ContainsValue(object? value, string expected)
    {
        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            return string.Equals(text, expected, StringComparison.Ordinal);
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary.Values.Any(child => ContainsValue(child, expected));
        }

        if (value is IEnumerable<object?> list)
        {
            return list.Any(child => ContainsValue(child, expected));
        }

        return false;
    }

    private sealed class CapturingAuditOutboxWriter : IAuditOutboxWriter
    {
        public bool EnqueueResult { get; init; } = true;
        public Exception? ExceptionToThrow { get; init; }
        public AuditOutboxWriteRequest? Request { get; private set; }

        public Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
        {
            Request = request;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(EnqueueResult);
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; init; } = Guid.Parse("b72ded0b-43c8-4e95-8158-bd56e391deaa");
        public string? Email { get; init; } = "admin@diten.com";
        public string? DisplayName { get; init; } = "Platform Admin";
        public string ActorName => Email ?? DisplayName ?? "system";
        public bool IsAuthenticated { get; init; } = true;
    }

    private sealed class InMemoryAuditRetentionPolicyRepository : IAuditRetentionPolicyRepository
    {
        private readonly IReadOnlyList<AuditEventRetentionPolicy> _policies;

        public InMemoryAuditRetentionPolicyRepository(IReadOnlyList<AuditEventRetentionPolicy> policies)
        {
            _policies = policies;
        }

        public Task<AuditEventRetentionPolicy?> GetActivePolicyAsync(AuditCategory category, string planTierCode, CancellationToken ct = default)
        {
            var policy = _policies.FirstOrDefault(item =>
                item.Category == category
                && string.Equals(item.PlanTierCode, planTierCode, StringComparison.OrdinalIgnoreCase)
                && item.IsActive
                && !item.IsDeleted);

            return Task.FromResult(policy);
        }

        public Task<AuditEventRetentionPolicy?> GetActivePolicyByIdAsync(Guid id, CancellationToken ct = default)
        {
            var policy = _policies.FirstOrDefault(item => item.Id == id && item.IsActive && !item.IsDeleted);
            return Task.FromResult(policy);
        }

        public Task<AuditEventRetentionPolicy?> GetDefaultPolicyAsync(AuditCategory category, CancellationToken ct = default)
        {
            return GetActivePolicyAsync(category, AuditEventRetentionPolicy.DefaultPlanTierCode, ct);
        }

        public Task<IReadOnlyList<AuditEventRetentionPolicy>> GetActivePoliciesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_policies.Where(item => item.IsActive && !item.IsDeleted).ToList() as IReadOnlyList<AuditEventRetentionPolicy>);
        }

        public Task<bool> UpdateAsync(AuditEventRetentionPolicy policy, CancellationToken ct = default)
        {
            return Task.FromResult(_policies.Any(item =>
                item.Id == policy.Id
                && item.Category == policy.Category
                && string.Equals(item.PlanTierCode, policy.PlanTierCode, StringComparison.OrdinalIgnoreCase)
                && !item.IsDeleted));
        }
    }

    private sealed class InMemoryTenantAuditPreferenceRepository : ITenantAuditPreferenceRepository
    {
        private IReadOnlyList<TenantAuditPreference> _preferences;

        public InMemoryTenantAuditPreferenceRepository(IReadOnlyList<TenantAuditPreference> preferences)
        {
            _preferences = preferences;
        }

        public void SetPreferences(IReadOnlyList<TenantAuditPreference> preferences)
        {
            _preferences = preferences;
        }

        public Task<TenantAuditPreference?> GetByTenantAndCategoryAsync(Guid tenantId, AuditCategory category, CancellationToken ct = default)
        {
            var preference = _preferences.FirstOrDefault(item =>
                item.TenantId == tenantId
                && item.Category == category
                && !item.IsDeleted);

            return Task.FromResult(preference);
        }

        public Task UpsertAsync(TenantAuditPreference preference, AuditEventRetentionPolicy policy, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
