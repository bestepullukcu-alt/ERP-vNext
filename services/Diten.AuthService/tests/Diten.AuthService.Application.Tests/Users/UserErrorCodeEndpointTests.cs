using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ERROR-CODES-01 (BL-450) — the code as it leaves the building. HTTP round trips through the REAL Api
/// (routing, model binding, [HasPermission], the pipeline, JSON serialization) on a test-owned mongod: every refusal
/// of a Users-screen command must carry its stable code in the envelope's <c>errorCodes[0].code</c>, next to the
/// English sentence in <c>errors</c>. Handler tests cannot see a code lost in binding or serialization — that escaped
/// this repo twice. A disposable tenant per class; the caller holds no steward role, so the last-steward case is real.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UserErrorCodeEndpointTests : IClassFixture<PlatformEdgeTestHost>
{
    private readonly PlatformEdgeTestHost _host;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId;
    private readonly Guid _stewardId;
    private readonly string _token;

    public UserErrorCodeEndpointTests(PlatformEdgeTestHost host)
    {
        _host = host;
        (_actorId, _stewardId, _token) = SeedAsync().GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("DELETE", "api/users/{0}")]
    [InlineData("PUT", "api/users/{0}")]
    [InlineData("POST", "api/users/{0}/disable")]
    [InlineData("POST", "api/users/{0}/enable")]
    [InlineData("POST", "api/users/resend-invite/{0}")]
    [InlineData("POST", "api/users/{0}/reset-password")]
    public async Task A_missing_user_is_404_USER_NOT_FOUND_on_every_door(string method, string route)
    {
        using var client = _host.Client(_token, _tenantId);
        using var request = new HttpRequestMessage(new HttpMethod(method), string.Format(route, Guid.NewGuid()));
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(new { firstName = "No", lastName = "Body", isActive = true });
        }

        await AssertRefusedAsync(await client.SendAsync(request), HttpStatusCode.NotFound, UserErrorCodes.NotFound);
    }

    [Fact]
    public async Task Deleting_yourself_is_409_USER_DELETE_SELF()
    {
        using var client = _host.Client(_token, _tenantId);

        await AssertRefusedAsync(await client.DeleteAsync($"api/users/{_actorId}"), HttpStatusCode.Conflict, UserErrorCodes.DeleteSelf);
    }

    [Fact]
    public async Task Deleting_the_last_account_that_can_create_users_is_409_USER_DELETE_LAST_STEWARD()
    {
        using var client = _host.Client(_token, _tenantId);

        await AssertRefusedAsync(await client.DeleteAsync($"api/users/{_stewardId}"), HttpStatusCode.Conflict, UserErrorCodes.DeleteLastSteward);
    }

    [Fact]
    public async Task Deactivating_yourself_is_409_USER_DEACTIVATE_SELF_on_the_kebab_and_on_the_form()
    {
        using var client = _host.Client(_token, _tenantId);

        await AssertRefusedAsync(await client.PostAsync($"api/users/{_actorId}/disable", null), HttpStatusCode.Conflict, UserErrorCodes.DeactivateSelf);
        await AssertRefusedAsync(
            await client.PutAsJsonAsync($"api/users/{_actorId}", new { firstName = "Code", lastName = "Actor", isActive = false }),
            HttpStatusCode.Conflict, UserErrorCodes.DeactivateSelf);
    }

    [Fact]
    public async Task Activating_an_invited_account_is_409_USER_INVITATION_PENDING()
    {
        var invited = await NewUserAsync(invited: true);
        using var client = _host.Client(_token, _tenantId);

        await AssertRefusedAsync(await client.PostAsync($"api/users/{invited}/enable", null), HttpStatusCode.Conflict, UserErrorCodes.InvitationPending);
    }

    [Fact]
    public async Task Resending_to_a_user_who_already_set_a_password_is_409_USER_SETUP_ALREADY_COMPLETED()
    {
        var active = await NewUserAsync(invited: false);
        using var client = _host.Client(_token, _tenantId);

        await AssertRefusedAsync(await client.PostAsync($"api/users/resend-invite/{active}", null), HttpStatusCode.Conflict, UserErrorCodes.SetupAlreadyCompleted);
    }

    [Fact]
    public async Task Resetting_a_password_that_was_never_set_is_409_USER_PASSWORD_SETUP_PENDING()
    {
        var invited = await NewUserAsync(invited: true);
        using var client = _host.Client(_token, _tenantId);

        await AssertRefusedAsync(await client.PostAsync($"api/users/{invited}/reset-password", null), HttpStatusCode.Conflict, UserErrorCodes.PasswordSetupPending);
    }

    [Fact]
    public async Task Creating_a_user_with_a_taken_address_is_409_USER_EMAIL_TAKEN()
    {
        var email = $"twice.{Guid.NewGuid():N}@codes.test";
        using var client = _host.Client(_token, _tenantId);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("api/users", new { email, firstName = "First", lastName = "Time" })).StatusCode);

        await AssertRefusedAsync(
            await client.PostAsJsonAsync("api/users", new { email, firstName = "Second", lastName = "Time" }),
            HttpStatusCode.Conflict, UserErrorCodes.EmailTaken);
    }

    // ── the validators (the pipeline's door: the "Validation failed" body, not the envelope) ───────────

    public static TheoryData<string, string, string, string?, string> CreateValidatorCases() => new()
    {
        { "", "First", "Last", null, UserErrorCodes.EmailRequired },
        { "not-an-address", "First", "Last", null, UserErrorCodes.EmailInvalid },
        { "a@codes.test", "", "Last", null, UserErrorCodes.FirstNameRequired },
        { "a@codes.test", new string('a', 101), "Last", null, UserErrorCodes.FirstNameTooLong },
        { "a@codes.test", "First", "", null, UserErrorCodes.LastNameRequired },
        { "a@codes.test", "First", new string('b', 101), null, UserErrorCodes.LastNameTooLong },
        { "a@codes.test", "First", "Last", "Robot", UserErrorCodes.AccountKindInvalid },
    };

    [Theory]
    [MemberData(nameof(CreateValidatorCases))]
    public async Task A_create_the_validator_refuses_is_400_with_its_code(string email, string firstName, string lastName, string? accountKind, string code)
    {
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PostAsJsonAsync("api/users", new { email, firstName, lastName, accountKind });

        Assert.Equal(code, (await ValidatorCodesAsync(response))[0]);
    }

    [Theory]
    [InlineData("", "Last", null, UserErrorCodes.FirstNameRequired)]
    [InlineData("First", "", null, UserErrorCodes.LastNameRequired)]
    [InlineData("First", "Last", "Robot", UserErrorCodes.AccountKindInvalid)]
    public async Task An_update_the_validator_refuses_is_400_with_its_code(string firstName, string lastName, string? accountKind, string code)
    {
        var target = await NewUserAsync(invited: false);
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PutAsJsonAsync($"api/users/{target}", new { firstName, lastName, isActive = true, accountKind });

        Assert.Equal(code, (await ValidatorCodesAsync(response))[0]);
    }

    /// <summary>
    /// The rule itself, on the wire: a validator code with a prefix on the one list (EnvelopeErrorCodePrefixes) is in
    /// the body's <c>errorCodes</c>; FluentValidation's own default code ("NotEmptyValidator", the Id rule) is not —
    /// that body has no <c>errorCodes</c> at all.
    /// </summary>
    [Fact]
    public async Task A_validator_code_with_an_allowed_prefix_reaches_the_wire_and_an_unprefixed_one_does_not()
    {
        using var client = _host.Client(_token, _tenantId);

        var coded = await client.PostAsJsonAsync("api/users", new { email = "a@codes.test", firstName = "", lastName = "" });
        var codes = await ValidatorCodesAsync(coded);
        Assert.Equal([UserErrorCodes.FirstNameRequired, UserErrorCodes.LastNameRequired], codes);
        Assert.All(codes, code => Assert.True(EnvelopeErrorCodePrefixes.Allows(code)));

        var uncoded = await client.PutAsJsonAsync($"api/users/{Guid.Empty}", new { firstName = "Still", lastName = "Valid", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, uncoded.StatusCode);
        using var doc = JsonDocument.Parse(await uncoded.Content.ReadAsStringAsync());
        Assert.Equal("Validation failed", doc.RootElement.GetProperty("title").GetString());
        Assert.False(doc.RootElement.TryGetProperty("errorCodes", out _), "a FluentValidation default code travelled");
    }

    /// <summary>Both doors write an errorCodes item the same way: the same property names, the code a string.</summary>
    [Fact]
    public async Task The_validator_door_and_the_envelope_door_write_the_same_errorCodes_item()
    {
        using var client = _host.Client(_token, _tenantId);

        var envelope = await client.DeleteAsync($"api/users/{Guid.NewGuid()}");
        var validator = await client.PostAsJsonAsync("api/users", new { email = "a@codes.test", firstName = "", lastName = "Last" });

        using var envelopeDoc = JsonDocument.Parse(await envelope.Content.ReadAsStringAsync());
        using var validatorDoc = JsonDocument.Parse(await validator.Content.ReadAsStringAsync());
        var envelopeItem = envelopeDoc.RootElement.GetProperty("errorCodes")[0];
        var validatorItem = validatorDoc.RootElement.GetProperty("errorCodes")[0];
        Assert.Equal(
            envelopeItem.EnumerateObject().Select(p => (p.Name, p.Value.ValueKind)).ToArray(),
            validatorItem.EnumerateObject().Select(p => (p.Name, p.Value.ValueKind)).ToArray());
        Assert.Equal(JsonValueKind.String, validatorItem.GetProperty("code").ValueKind);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>400 "Validation failed" with its four fields intact, plus the codes — returned in order.</summary>
    private static async Task<string[]> ValidatorCodesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"expected 400, got {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("Validation failed", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("errorCodes", out var codes) && codes.ValueKind == JsonValueKind.Array && codes.GetArrayLength() > 0,
            $"the validator's refusal left without a code: {body}");
        return codes.EnumerateArray().Select(c => c.GetProperty("code").GetString()!).ToArray();
    }

    private static async Task AssertRefusedAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"expected {(int)status}, got {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("isSuccessful").GetBoolean());
        Assert.True(root.TryGetProperty("errorCodes", out var codes) && codes.ValueKind == JsonValueKind.Array && codes.GetArrayLength() > 0,
            $"the refusal left without a code — the envelope has no errorCodes: {body}");
        Assert.Equal(code, codes[0].GetProperty("code").GetString());
        // The English sentence stays for API consumers and logs.
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("errors")[0].GetString()));
    }

    private async Task<Guid> NewUserAsync(bool invited)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = new User($"u.{Guid.NewGuid():N}@codes.test", "hash:x", "Sub", "Ject", _tenantId);
        user.SetAccountKind(AccountKind.Human);
        if (invited)
        {
            user.RequirePasswordChange(null);
            user.Deactivate();
        }
        else
        {
            user.ConfirmEmail();
        }

        return (await users.CreateAsync(user, CancellationToken.None)).Id;
    }

    /// <summary>
    /// The caller carries the write keys in the token but holds NO role; the one role with <c>auth.users.create</c>
    /// belongs to a second account — which makes that account the tenant's last steward.
    /// </summary>
    private async Task<(Guid ActorId, Guid StewardId, string Token)> SeedAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var tokens = sp.GetRequiredService<ITokenService>();

        var actor = new User($"actor.{Guid.NewGuid():N}@codes.test", "hash:x", "Code", "Actor", _tenantId);
        actor.ConfirmEmail();
        actor = await users.CreateAsync(actor, CancellationToken.None);

        var steward = new User($"steward.{Guid.NewGuid():N}@codes.test", "hash:x", "Last", "Steward", _tenantId);
        steward.ConfirmEmail();
        steward = await users.CreateAsync(steward, CancellationToken.None);

        var stewards = await roles.CreateAsync(new Role("Stewards", "Stewards", "error-code fixture", _tenantId), CancellationToken.None);
        var create = await _host.Database.GetCollection<Permission>("permissions")
            .Find(p => p.Key == "auth.users.create").SingleAsync();
        await _host.Database.GetCollection<RolePermission>("rolePermissions")
            .InsertOneAsync(RolePermission.ManualGrant(stewards.Id, create.Id, _tenantId, "error-code-fixture"));
        await userRoles.AssignAsync(new UserRole(steward.Id, stewards.Id, _tenantId, "error-code-fixture"), CancellationToken.None);

        var token = tokens.GenerateAccessToken(actor, [],
            ["auth.users.read", "auth.users.create", "auth.users.update", "auth.users.delete"], expiresInMinutes: 60);
        return (actor.Id, steward.Id, token);
    }
}
