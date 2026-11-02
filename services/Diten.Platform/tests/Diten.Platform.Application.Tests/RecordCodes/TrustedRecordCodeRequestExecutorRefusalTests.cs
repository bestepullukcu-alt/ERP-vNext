using System.Text;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 AC-1 / AC-41 — <c>InternalRecordCodesController</c> is <c>[AllowAnonymous]</c>: the framework lets every
/// request in and <see cref="TrustedRecordCodeRequestExecutor"/> is the whole gate. These are the proofs the LoginOnly guard
/// asks for before an anonymous internal controller is listed: each refusal of steps 1–2 is driven through the REAL
/// credential authenticator and the REAL token context (explicit JWT bearer validation), and each asserts its status and
/// reason code and that the action — the MediatR handler — never ran and no tenant scope was opened.
/// </summary>
public sealed class TrustedRecordCodeRequestExecutorRefusalTests
{
    private static readonly Guid Tenant = Guid.Parse("40fc0002-0000-4000-8000-0000000000a2");
    private static readonly Guid User = Guid.Parse("40fc0002-0000-4000-8000-0000000000b2");


    // ── step 1: size ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_body_above_4_KB_is_413_before_the_credential_is_read()
    {
        var context = Context();
        context.Request.ContentLength = TrustedRecordCodeRequestExecutor.MaxBodyBytes + 1;
        context.Request.Headers.Remove(TrustedRecordCodeRequestExecutor.CredentialIdHeader);

        var result = await ExecuteAsync(context);

        Assert.Equal((413, "RECORD_CODE_REQUEST_TOO_LARGE"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    // ── step 2: credential headers ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TrustedRecordCodeRequestExecutor.CredentialIdHeader)]
    [InlineData(TrustedRecordCodeRequestExecutor.CredentialSecretHeader)]
    [InlineData(TrustedRecordCodeRequestExecutor.AudienceHeader)]
    public async Task A_missing_credential_header_is_401(string missing)
    {
        var context = Context();
        context.Request.Headers.Remove(missing);

        var result = await ExecuteAsync(context);

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Theory]
    [InlineData(TrustedRecordCodeRequestExecutor.CredentialIdHeader)]
    [InlineData(TrustedRecordCodeRequestExecutor.CredentialSecretHeader)]
    [InlineData(TrustedRecordCodeRequestExecutor.AudienceHeader)]
    public async Task A_duplicated_credential_header_is_401(string duplicated)
    {
        var context = Context();
        context.Request.Headers[duplicated] = new[] { "a", "b" };

        var result = await ExecuteAsync(context);

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    // ── step 2: the credential itself (pack §8.2 table, FU21 mapping) ────────────────────────────────────────

    [Theory]
    [InlineData("unknown-identifier", Secret)]
    [InlineData(Identifier, "wrong-secret")]
    public async Task A_credential_that_does_not_authenticate_is_401(string identifier, string secret)
    {
        var context = Context(identifier: identifier, secret: secret);

        var result = await ExecuteAsync(context);

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task A_revoked_credential_is_401_not_403()
    {
        var options = Options();
        options.FinanceTreasury.IsRevoked = true;

        var result = await ExecuteAsync(Context(), options);

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task A_wrong_audience_header_is_403()
    {
        var result = await ExecuteAsync(Context(audience: "TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE"));

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task A_binding_whose_consumer_service_is_not_its_own_is_403()
    {
        var options = Options();
        options.FinanceTreasury.ConsumerService = "DITENMDMSERVICE";

        var result = await ExecuteAsync(Context(), options);

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    public static TheoryData<string> InvalidConfigurations => new()
    {
        "no-binding", "duplicated-type", "type-outside-the-binding", "no-type", "same-identifier-twice", "no-secret"
    };

    /// <summary>A configuration outside the closed consumer map refuses every request 403 (pack §8.2).</summary>
    [Theory]
    [MemberData(nameof(InvalidConfigurations))]
    public async Task An_invalid_configuration_is_403_for_every_request(string invalid)
    {
        var options = Options();
        switch (invalid)
        {
            case "no-binding":
                options.FinanceTreasury = new TrustedRecordCodeCredentialBinding();
                options.Mdm = new TrustedRecordCodeCredentialBinding();
                break;
            case "duplicated-type":
                options.FinanceTreasury.AllowedRecordTypes = ["PAY", "PAY"];
                break;
            case "type-outside-the-binding":
                options.FinanceTreasury.AllowedRecordTypes = ["PAY", "BNK"];
                break;
            case "no-type":
                options.FinanceTreasury.AllowedRecordTypes = [];
                break;
            case "same-identifier-twice":
                options.Mdm.Identifier = Identifier;
                break;
            case "no-secret":
                options.Mdm.ActiveSecret = string.Empty;
                break;
        }

        var result = await ExecuteAsync(Context(), options);

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    // ── step 2: the forwarded user token ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_user_token_is_401()
    {
        var context = Context();
        context.Request.Headers.Remove("Authorization");

        var result = await ExecuteAsync(context);

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task An_invalid_user_token_is_401()
    {
        var context = Context(bearer: TokenSignedWith("another key entirely, test only, 0123456789abcdef"));

        var result = await ExecuteAsync(context);

        Assert.Equal((401, "RECORD_CODE_UNAUTHENTICATED"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Fact]
    public async Task An_X_Tenant_Id_header_is_403()
    {
        var context = Context();
        context.Request.Headers["X-Tenant-Id"] = Tenant.ToString();

        var result = await ExecuteAsync(context);

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    public static TheoryData<string?, string?, string?> TokensWithoutATenantUser => new()
    {
        // actor_type, tenant_id, sub
        { "tenant_user", null, User.ToString() },
        { "tenant_user", "not-a-guid", User.ToString() },
        { "tenant_user", Guid.Empty.ToString(), User.ToString() },
        { "tenant_user", Tenant.ToString(), null },
        { "tenant_user", Tenant.ToString(), "not-a-guid" },
        { "tenant_user", Tenant.ToString(), Guid.Empty.ToString() },
        { null, Tenant.ToString(), User.ToString() }
    };

    [Theory]
    [MemberData(nameof(TokensWithoutATenantUser))]
    public async Task A_token_without_a_tenant_or_a_subject_is_403(string? actorType, string? tenant, string? subject)
    {
        var context = Context(bearer: RecordCodeHttpHost.Token(actorType, tenant, subject));

        var result = await ExecuteAsync(context);

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    [InlineData("service")]
    public async Task A_token_whose_actor_type_is_not_tenant_user_is_403(string actorType)
    {
        var context = Context(bearer: RecordCodeHttpHost.Token(actorType, Tenant.ToString(), User.ToString()));

        var result = await ExecuteAsync(context);

        Assert.Equal((403, "RECORD_CODE_FORBIDDEN"), result.Refusal);
        Assert.False(result.ActionRan);
    }

    /// <summary>The control: the same gate, everything right, reaches the action — inside the token tenant's scope.</summary>
    [Fact]
    public async Task A_valid_credential_and_tenant_user_token_reach_the_action_in_the_tokens_tenant()
    {
        var context = Context(bearer: RecordCodeHttpHost.TenantUserToken(Tenant, User));

        var result = await ExecuteAsync(context);

        Assert.Null(result.Refusal);
        Assert.True(result.ActionRan);
        Assert.Equal(Tenant, result.Caller!.TenantId);
        Assert.Equal(User, result.Caller.SubjectId);
        Assert.Equal("Diten.FinanceTreasuryService", result.Caller.CallerService);
        Assert.Equal(Tenant, result.TenantInScope);
        Assert.Equal(User.ToString(), context.User.FindFirst("sub")?.Value);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────────

    private const string Identifier = RecordCodeHttpHost.FinanceIdentifier;
    private const string Secret = RecordCodeHttpHost.FinanceSecret;

    private sealed record Outcome((int Status, string Code)? Refusal, bool ActionRan, TrustedRecordCodeCaller? Caller, Guid? TenantInScope);

    private static TrustedRecordCodeCredentialOptions Options() => new()
    {
        FinanceTreasury = RecordCodeHttpHost.Binding(Identifier, Secret, "DITENFINANCETREASURYSERVICE", "PAY", "APD", "ARI", "CPS", "CTP"),
        Mdm = RecordCodeHttpHost.Binding(RecordCodeHttpHost.MdmIdentifier, RecordCodeHttpHost.MdmSecret, "DITENMDMSERVICE", "BNK", "BAC", "AGR")
    };

    private static async Task<Outcome> ExecuteAsync(
        DefaultHttpContext context,
        TrustedRecordCodeCredentialOptions? options = null)
    {
        context.RequestServices = Services();
        var tenantContext = new TenantContext();
        (int, string)? refusal = null;
        var actionRan = false;
        TrustedRecordCodeCaller? caller = null;
        Guid? tenantInScope = null;

        await new TrustedRecordCodeRequestExecutor(
                new TrustedRecordCodeCredentialAuthenticator(
                    Microsoft.Extensions.Options.Options.Create(options ?? Options()), TimeProvider.System),
                new TrustedRecordCodeJwtContext(),
                tenantContext)
            .ExecuteAsync(
                context,
                default,
                (who, _) =>
                {
                    actionRan = true;
                    caller = who;
                    tenantInScope = tenantContext.IsResolved ? tenantContext.TenantId : null;
                    return Task.FromResult<IActionResult>(new OkResult());
                },
                (status, code) =>
                {
                    refusal = (status, code);
                    return new StatusCodeResult(status);
                });

        if (refusal is not null)
        {
            Assert.False(tenantContext.IsResolved);
        }

        return new Outcome(refusal, actionRan, caller, tenantInScope);
    }

    /// <summary>The JWT bearer scheme the executor validates the forwarded token under (the host's Platform settings).</summary>
    private static IServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = RecordCodeHttpHost.ValidationParameters();
            });
        return services.BuildServiceProvider();
    }

    private static string TokenSignedWith(string key)
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: RecordCodeHttpHost.Issuer,
            audience: RecordCodeHttpHost.Audience,
            claims: [new("sub", User.ToString()), new("tenant_id", Tenant.ToString()), new("actor_type", "tenant_user")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private static DefaultHttpContext Context(
        string identifier = Identifier,
        string secret = Secret,
        string audience = TrustedRecordCodeCredentialAuthenticator.Audience,
        string? bearer = null)
    {
        var bytes = Encoding.UTF8.GetBytes("{\"recordType\":\"PAY\",\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"idempotencyKey\":\"k-1\"}");
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        context.Request.Headers[TrustedRecordCodeRequestExecutor.CredentialIdHeader] = identifier;
        context.Request.Headers[TrustedRecordCodeRequestExecutor.CredentialSecretHeader] = secret;
        context.Request.Headers[TrustedRecordCodeRequestExecutor.AudienceHeader] = audience;
        context.Request.Headers["Authorization"] = "Bearer " + (bearer ?? RecordCodeHttpHost.TenantUserToken(Tenant, User));
        return context;
    }
}
