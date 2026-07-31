using System.Security.Claims;
using System.Text.Json;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.ExternalContextReferences;
using Diten.PpmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class ExternalContextReferenceContractTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ContextId = Guid.Parse("abcdef12-3333-3333-3333-333333333333");

    [Theory]
    [InlineData("Portfolio", "ppm.portfolios.read")]
    [InlineData("Initiative", "ppm.initiatives.read")]
    [InlineData("Program", "ppm.programs.read")]
    [InlineData("Project", "ppm.projects.read")]
    public void Exact_context_kind_maps_only_to_canonical_read_permission(string kind, string permission)
    {
        Assert.Equal(permission, ExternalContextReferenceContract.PermissionFor(kind));
    }

    [Theory]
    [InlineData("portfolio")]
    [InlineData("PROJECT")]
    [InlineData("Project ")]
    [InlineData("Demand")]
    [InlineData("*")]
    public void Unknown_or_case_changed_context_kind_is_rejected(string kind)
    {
        Assert.Null(ExternalContextReferenceContract.PermissionFor(kind));
    }

    [Theory]
    [InlineData("ppm.portfolios.view")]
    [InlineData("ppm.projects.*")]
    [InlineData("ppm.programs")]
    [InlineData("PPM.INITIATIVES.READ")]
    public void View_alias_wildcard_prefix_and_case_variants_are_not_mappings(string permission)
    {
        Assert.DoesNotContain(permission, new[]
        {
            ExternalContextReferenceContract.PermissionFor("Portfolio"),
            ExternalContextReferenceContract.PermissionFor("Initiative"),
            ExternalContextReferenceContract.PermissionFor("Program"),
            ExternalContextReferenceContract.PermissionFor("Project")
        });
    }

    [Fact]
    public void Validator_accepts_only_exact_closed_contract_and_canonical_lowercase_guid()
    {
        var validator = new ValidateExternalContextReferenceValidator();
        var valid = Query("Project", ContextId.ToString("D"));
        Assert.True(validator.Validate(valid).IsValid);

        Assert.False(validator.Validate(valid with { ContractName = "other" }).IsValid);
        Assert.False(validator.Validate(valid with { ContractVersion = "1" }).IsValid);
        Assert.False(validator.Validate(valid with { ContextId = ContextId.ToString("B") }).IsValid);
        Assert.False(validator.Validate(valid with { ContextId = ContextId.ToString("D").ToUpperInvariant() }).IsValid);
        Assert.False(validator.Validate(valid with { ContextId = Guid.Empty.ToString("D") }).IsValid);
        Assert.False(validator.Validate(valid with { HasAdditionalProperties = true }).IsValid);
    }

    [Fact]
    public void Success_serializes_exactly_four_contract_fields()
    {
        var response = new ExternalContextReferenceResponse(
            ExternalContextReferenceContract.Name,
            ExternalContextReferenceContract.Version,
            "Project",
            ContextId.ToString("D"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        Assert.Equal(
            ["contextId", "contextKind", "contractName", "contractVersion"],
            json.RootElement.EnumerateObject().Select(x => x.Name).Order().ToArray());
    }

    [Fact]
    public void Unknown_body_fields_are_captured_for_fail_closed_validation()
    {
        var request = JsonSerializer.Deserialize<ValidateExternalContextReferenceRequest>(
            $$"""{"contractName":"ppm.external-context-reference","contractVersion":"1.0","contextKind":"Project","contextId":"{{ContextId:D}}","tenantId":"{{TenantId:D}}"}""");

        Assert.NotNull(request);
        Assert.True(request.AdditionalProperties?.ContainsKey("tenantId"));
    }

    [Fact]
    public void Strict_context_allows_name_identifier_only_when_sub_is_absent()
    {
        var fallback = Principal(
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim(ClaimTypes.NameIdentifier, ActorId.ToString("D")));
        Assert.True(ExternalContextProviderSecurityFilter.TryResolveStrictContext(
            fallback, out var tenant, out var actor));
        Assert.Equal(TenantId, tenant);
        Assert.Equal(ActorId, actor);

        var malformedSub = Principal(
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("sub", "invalid"),
            new Claim(ClaimTypes.NameIdentifier, ActorId.ToString("D")));
        Assert.False(ExternalContextProviderSecurityFilter.TryResolveStrictContext(
            malformedSub, out _, out _));
    }

    [Theory]
    [InlineData(null, "22222222-2222-2222-2222-222222222222")]
    [InlineData("", "22222222-2222-2222-2222-222222222222")]
    [InlineData("00000000-0000-0000-0000-000000000000", "22222222-2222-2222-2222-222222222222")]
    [InlineData("11111111-1111-1111-1111-111111111111", null)]
    [InlineData("11111111-1111-1111-1111-111111111111", "invalid")]
    [InlineData("11111111-1111-1111-1111-111111111111", "00000000-0000-0000-0000-000000000000")]
    public void Strict_context_rejects_missing_malformed_or_empty_tenant_and_subject(
        string? tenant,
        string? subject)
    {
        var claims = new List<Claim>();
        if (tenant is not null) claims.Add(new("tenant_id", tenant));
        if (subject is not null) claims.Add(new("sub", subject));
        Assert.False(ExternalContextProviderSecurityFilter.TryResolveStrictContext(
            Principal(claims.ToArray()), out _, out _));
    }

    [Fact]
    public async Task Handler_enforces_access_before_lookup_and_uses_exact_permission()
    {
        var lookup = new CountingLookup(new(true, null));
        var authorizer = new RecordingAuthorizer(PpmAccessDecision.Allowed);
        var handler = new ValidateExternalContextReferenceHandler(
            new TenantContext(), authorizer, lookup);

        var result = await handler.Handle(Query("Project", ContextId.ToString("D")), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PpmPermissions.ProjectsRead, authorizer.Permission);
        Assert.Equal(1, lookup.Calls);
        Assert.Equal(ContextId.ToString("D"), result.Data?.ContextId);
    }

    [Theory]
    [InlineData(PpmAccessDecision.Forbidden, 403)]
    [InlineData(PpmAccessDecision.DependencyUnavailable, 503)]
    public async Task Access_failure_never_calls_repository(PpmAccessDecision decision, int statusCode)
    {
        var lookup = new CountingLookup(new(true, null));
        var handler = new ValidateExternalContextReferenceHandler(
            new TenantContext(), new RecordingAuthorizer(decision), lookup);

        var result = await handler.Handle(Query("Portfolio", ContextId.ToString("D")), default);

        Assert.Equal(statusCode, result.StatusCode);
        Assert.Equal(0, lookup.Calls);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "restricted-policy")]
    public async Task Non_referenceable_or_non_null_visibility_is_indistinguishable_404(
        bool isReferenceable,
        string? visibilityPolicyKey)
    {
        var handler = new ValidateExternalContextReferenceHandler(
            new TenantContext(),
            new RecordingAuthorizer(PpmAccessDecision.Allowed),
            new CountingLookup(new(isReferenceable, visibilityPolicyKey)));

        var result = await handler.Handle(Query("Initiative", ContextId.ToString("D")), default);

        Assert.Equal(404, result.StatusCode);
        Assert.DoesNotContain("policy", string.Join(' ', result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dependency_failure_maps_to_503()
    {
        var handler = new ValidateExternalContextReferenceHandler(
            new TenantContext(),
            new RecordingAuthorizer(PpmAccessDecision.Allowed),
            new ThrowingLookup());

        var result = await handler.Handle(Query("Program", ContextId.ToString("D")), default);
        Assert.Equal(503, result.StatusCode);
    }

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(true, "short", false)]
    [InlineData(true, "this-is-a-placeholder-secret", false)]
    [InlineData(true, "T8kQ3nLm7vPx2sWr9cYf6hZa", true)]
    public void Provider_activation_secret_validation_is_conditional(
        bool enabled,
        string? credential,
        bool valid)
    {
        var result = new ExternalContextProviderOptionsValidator().Validate(null, new()
        {
            Enabled = enabled,
            ServiceCredential = credential
        });
        Assert.Equal(valid, result.Succeeded);
    }

    private static ValidateExternalContextReferenceQuery Query(string kind, string id) => new(
        ExternalContextReferenceContract.Name,
        ExternalContextReferenceContract.Version,
        kind,
        id,
        false);

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    private sealed class TenantContext : ITenantContext
    {
        public Guid TenantId => ExternalContextReferenceContractTests.TenantId;
    }

    private sealed class RecordingAuthorizer(PpmAccessDecision decision) : IPpmAccessAuthorizer
    {
        public string? Permission { get; private set; }
        public Task<PpmAccessDecision> AuthorizeAsync(string permission, CancellationToken cancellationToken)
        {
            Permission = permission;
            return Task.FromResult(decision);
        }
    }

    private sealed class CountingLookup(ExternalContextReferenceLookupResult? result)
        : IExternalContextReferenceLookup
    {
        public int Calls { get; private set; }
        public Task<ExternalContextReferenceLookupResult?> FindAsync(
            Guid tenantId, string contextKind, Guid contextId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingLookup : IExternalContextReferenceLookup
    {
        public Task<ExternalContextReferenceLookupResult?> FindAsync(
            Guid tenantId, string contextKind, Guid contextId, CancellationToken cancellationToken) =>
            throw new ExternalContextReferenceDependencyException("unavailable", new TimeoutException());
    }
}
