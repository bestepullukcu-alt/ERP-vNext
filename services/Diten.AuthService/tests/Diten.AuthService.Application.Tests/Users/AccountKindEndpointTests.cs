using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Settings;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-ACCOUNT-KIND-01 — K4 (isolated real provider) and K5 (live, isolated) evidence in one place.
///
/// <para>Every assertion here is an HTTP round trip through the REAL Api (WebApplicationFactory) against a
/// test-owned mongod (EphemeralMongo). Nothing is mocked: JWT validation, tenant resolution, [HasPermission],
/// FluentValidation, the Response envelope, the Mongo repository and the audit write are all the production code.
/// The subjects and actors are the disposable tenant seeded by <see cref="AccountKindAcceptance"/>; the
/// DefaultTenant and every pre-existing account are untouched (PPM boundary 1).</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class AccountKindEndpointTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;
    private readonly AccountKindAcceptance.Seed _seed;

    public AccountKindEndpointTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
        _seed = host.Seeded;
    }

    // ── the fixture itself is what it claims to be ──────────────────────────────────────────────────────

    [Fact]
    public void Host_runs_on_the_ephemeral_mongod_not_the_shared_localhost_server()
    {
        var settings = _host.Factory.Services.GetRequiredService<MongoDbSettings>();

        Assert.Equal(_host.ConnectionString, settings.ConnectionString);
        Assert.Equal(AccountKindAcceptance.DatabaseName, settings.DatabaseName);
        Assert.DoesNotContain(":27017", _host.ConnectionString); // never the shared dev server
        // The catalog rows the seeder wrote are in THIS database — proof the production seed ran here, nowhere else.
        var catalog = _host.Database.GetCollection<Permission>("permissions");
        Assert.NotNull(catalog.Find(p => p.Key == "auth.users.lookup").FirstOrDefault());
        Assert.NotNull(catalog.Find(p => p.Key == "auth.users.account-kind.manage").FirstOrDefault());
    }

    [Fact]
    public async Task Dead_client_fails_with_a_connection_error_not_a_green()
    {
        using var dead = AccountKindAcceptance.AuthTestHost.DeadClient();

        await Assert.ThrowsAsync<HttpRequestException>(() => dead.GetAsync($"api/users/{_seed.Human.Id}/account-assertion"));
    }

    // ── account-assertion: four bodies + the two 404s that must not differ ──────────────────────────────

    [Theory]
    [InlineData("Human", true, "Human")]
    [InlineData("Unknown", true, "Unknown")]
    [InlineData("Service", true, "Service")]
    [InlineData("Passive", false, "Human")]
    public async Task Assertion_reports_the_fact_for_each_subject(string subject, bool expectedActive, string expectedKind)
    {
        var user = subject switch
        {
            "Human" => _seed.Human,
            "Unknown" => _seed.Unknown,
            "Service" => _seed.Service,
            _ => _seed.Passive
        };
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{user.Id}/account-assertion");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        Assert.True(doc.RootElement.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal(user.Id, data.GetProperty("userId").GetGuid());
        Assert.Equal(expectedActive, data.GetProperty("active").GetBoolean());
        Assert.Equal(expectedKind, data.GetProperty("accountKind").GetString()); // the NAME, never the number
        Assert.Equal(JsonValueKind.String, data.GetProperty("assertedAt").ValueKind);
        Assert.True(data.TryGetProperty("userUpdatedAt", out _));
        // The assertion discloses no identity beyond the id: no email, no name, no roles.
        Assert.False(data.TryGetProperty("email", out _));
        Assert.False(data.TryGetProperty("roles", out _));
    }

    [Fact]
    public async Task Assertion_404_is_byte_identical_for_a_missing_user_and_another_tenants_user()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var missing = await client.GetAsync($"api/users/{Guid.NewGuid()}/account-assertion");
        var foreign = await client.GetAsync($"api/users/{_seed.Foreign.Id}/account-assertion");
        var missingBody = await missing.Content.ReadAsStringAsync();
        var foreignBody = await foreign.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(missingBody, foreignBody); // no cross-tenant existence disclosure
        Assert.Contains("User not found.", foreignBody);
    }

    // ── 401 / 403 ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Missing_or_garbage_token_is_401_on_every_new_endpoint()
    {
        using var anonymous = _host.Client(null, _seed.TenantId);
        using var garbage = _host.Client("not.a.jwt", _seed.TenantId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/users/lookup?search=a")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"api/users/{_seed.Human.Id}/account-assertion")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"api/users/{_seed.Mutable.Id}/account-kind", new { kind = "Human" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await garbage.GetAsync("api/users/lookup?search=a")).StatusCode);
    }

    [Fact]
    public async Task Actor_without_the_key_is_403_and_the_keys_are_not_interchangeable()
    {
        using var nobody = _host.Client(_seed.NoPermissionToken, _seed.TenantId);
        using var pmo = _host.Client(_seed.PmoToken, _seed.TenantId);
        using var kindAdmin = _host.Client(_seed.KindAdminToken, _seed.TenantId);

        // No permission at all → 403 everywhere.
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("api/users/lookup?search=a")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync($"api/users/{_seed.Human.Id}/account-assertion")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsJsonAsync($"api/users/{_seed.Mutable.Id}/account-kind", new { kind = "Human" })).StatusCode);

        // lookup does not imply manage …
        Assert.Equal(HttpStatusCode.Forbidden, (await pmo.PostAsJsonAsync($"api/users/{_seed.Mutable.Id}/account-kind", new { kind = "Human" })).StatusCode);
        // … and manage (+create +read) does not imply lookup.
        Assert.Equal(HttpStatusCode.Forbidden, (await kindAdmin.GetAsync("api/users/lookup?search=a")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await kindAdmin.GetAsync($"api/users/{_seed.Human.Id}/account-assertion")).StatusCode);
    }

    // ── lookup ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lookup_lists_only_active_users_of_the_callers_tenant_with_id_and_label_and_no_email()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync("api/users/lookup?limit=50");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("data").EnumerateArray().ToList();
        var ids = items.Select(i => i.GetProperty("userId").GetGuid()).ToHashSet();

        Assert.Contains(_seed.Human.Id, ids);
        Assert.Contains(_seed.Unknown.Id, ids);
        Assert.Contains(_seed.Service.Id, ids);
        Assert.DoesNotContain(_seed.Passive.Id, ids);   // IsActive filter (K1-d: drop it in the repository → red)
        Assert.DoesNotContain(_seed.Foreign.Id, ids);   // tenant filter
        Assert.All(items, i =>
        {
            Assert.Equal(2, i.EnumerateObject().Count()); // exactly userId + displayLabel
            Assert.False(i.TryGetProperty("email", out _));
        });
        Assert.DoesNotContain("@", body); // no e-mail address anywhere in the lookup body
        var human = items.Single(i => i.GetProperty("userId").GetGuid() == _seed.Human.Id);
        Assert.Equal($"{_seed.Human.FirstName} {_seed.Human.LastName}", human.GetProperty("displayLabel").GetString());
    }

    [Fact]
    public async Task Lookup_search_matches_first_or_last_name_tokens_and_respects_the_limit()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var bySurname = await (await client.GetAsync("api/users/lookup?search=yıld")).Content.ReadAsStringAsync();
        var byBoth = await (await client.GetAsync("api/users/lookup?search=zehra%20y")).Content.ReadAsStringAsync();
        var limited = await (await client.GetAsync("api/users/lookup?limit=1")).Content.ReadAsStringAsync();
        var byEmail = await (await client.GetAsync($"api/users/lookup?search={Uri.EscapeDataString(_seed.Human.Email)}")).Content.ReadAsStringAsync();

        Assert.Equal(new[] { _seed.Human.Id }, Ids(bySurname));
        Assert.Equal(new[] { _seed.Human.Id }, Ids(byBoth));
        Assert.Single(Ids(limited));
        Assert.Empty(Ids(byEmail)); // e-mail is not a match field: the lookup cannot be used to probe addresses
    }

    /*
     * THE 400 CONTRACT, AS MEASURED. AuthService registers ValidationBehavior FIRST — i.e. OUTERMOST — so a
     * validator failure never passes through ExceptionHandlingBehavior; it leaves the controller as a
     * FluentValidation ValidationException and GlobalExceptionHandler turns it into the service's standard
     * ProblemDetails: { title: "Validation failed", status: 400, detail: "<messages>", traceId }. That is the shape
     * every validator failure in this service has (CreateUser included) and the frontend already parses it. A
     * HANDLER-level refusal (permission, not-found, an undefined kind that slipped past the validator) answers
     * inside the Response envelope. Both are pinned below.
     */
    [Theory]
    [InlineData("api/users/lookup?limit=0", "Limit must be between 1 and 50")]
    [InlineData("api/users/lookup?limit=51", "Limit must be between 1 and 50")]
    public async Task Lookup_out_of_range_limit_is_the_validator_400(string path, string expectedMessage)
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertValidatorProblem(body, expectedMessage);
    }

    [Fact]
    public async Task Lookup_search_longer_than_100_characters_is_the_validator_400()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync("api/users/lookup?search=" + new string('a', 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertValidatorProblem(await response.Content.ReadAsStringAsync(), "Search may be at most 100 characters");
    }

    // ── account-kind (the write) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Kind_admin_classifies_an_unknown_account_and_the_change_is_audited_with_actor_target_tenant_and_correlation()
    {
        const string correlationId = "acc-kind-itest-0001";
        using var client = _host.Client(_seed.KindAdminToken, _seed.TenantId, correlationId);
        using var pmo = _host.Client(_seed.PmoToken, _seed.TenantId);
        var audit = _host.Database.GetCollection<AuthAuditLog>("authAuditLogs");
        var before = await audit.CountDocumentsAsync(a => a.EventName == SetAccountKindCommandHandler.AuditEventName && a.TenantId == _seed.TenantId);

        var response = await client.PostAsJsonAsync($"api/users/{_seed.Mutable.Id}/account-kind", new { kind = "human" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var doc = JsonDocument.Parse(body))
        {
            Assert.Equal("Human", doc.RootElement.GetProperty("data").GetProperty("accountKind").GetString());
        }

        // The fact changed for a reader too.
        var assertion = await (await pmo.GetAsync($"api/users/{_seed.Mutable.Id}/account-assertion")).Content.ReadAsStringAsync();
        using (var doc = JsonDocument.Parse(assertion))
        {
            Assert.Equal("Human", doc.RootElement.GetProperty("data").GetProperty("accountKind").GetString());
        }

        // K5 — the audit row, read straight from the isolated database.
        var rows = await audit.Find(a => a.EventName == SetAccountKindCommandHandler.AuditEventName && a.TenantId == _seed.TenantId).ToListAsync();
        Assert.Equal(before + 1, rows.Count);
        var row = rows.OrderByDescending(r => r.OccurredAt).First();
        Assert.Equal(_seed.KindAdmin.Id, row.UserId); // the row's UserId is the ACTOR
        using var meta = JsonDocument.Parse(row.Metadata);
        var m = meta.RootElement;
        Assert.Equal(_seed.KindAdmin.Id.ToString(), m.GetProperty("actorId").GetString());
        Assert.Equal(_seed.Mutable.Id.ToString(), m.GetProperty("targetUserId").GetString());
        Assert.Equal(_seed.TenantId.ToString(), m.GetProperty("tenantId").GetString());
        Assert.Equal("Unknown", m.GetProperty("previousKind").GetString());
        Assert.Equal("Human", m.GetProperty("newKind").GetString());
        Assert.Equal(correlationId, m.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Setting_the_kind_an_account_already_has_is_200_and_writes_no_audit_row()
    {
        using var client = _host.Client(_seed.KindAdminToken, _seed.TenantId);
        var audit = _host.Database.GetCollection<AuthAuditLog>("authAuditLogs");
        var before = await audit.CountDocumentsAsync(a => a.EventName == SetAccountKindCommandHandler.AuditEventName);

        var response = await client.PostAsJsonAsync($"api/users/{_seed.Service.Id}/account-kind", new { kind = "Service" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await audit.CountDocumentsAsync(a => a.EventName == SetAccountKindCommandHandler.AuditEventName));
    }

    [Fact]
    public async Task Unknown_kind_word_is_the_validator_400_and_a_broken_payload_is_the_frameworks_400()
    {
        using var client = _host.Client(_seed.KindAdminToken, _seed.TenantId);

        // A word that is not an enum name → FluentValidation → the service's "Validation failed" ProblemDetails.
        var wrongWord = await client.PostAsJsonAsync($"api/users/{_seed.Mutable.Id}/account-kind", new { kind = "Robot" });
        var wrongWordBody = await wrongWord.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, wrongWord.StatusCode);
        AssertValidatorProblem(wrongWordBody, "Kind must be one of: Unknown, Human, Service.");

        // Malformed JSON → model binding → the FRAMEWORK's 400 (ValidationProblemDetails with an `errors` map):
        // a different title, no envelope, no validator message.
        using var broken = new StringContent("{ \"kind\": ", Encoding.UTF8, "application/json");
        var malformed = await client.PostAsync($"api/users/{_seed.Mutable.Id}/account-kind", broken);
        var malformedBody = await malformed.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var doc = JsonDocument.Parse(malformedBody);
        Assert.False(doc.RootElement.TryGetProperty("isSuccessful", out _));
        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("errors").ValueKind);
        Assert.NotEqual("Validation failed", doc.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Kind must be one of", malformedBody);
    }

    /// <summary>The validator 400: GlobalExceptionHandler's ProblemDetails, never the Response envelope.</summary>
    private static void AssertValidatorProblem(string body, string expectedMessage)
    {
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Validation failed", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(expectedMessage, doc.RootElement.GetProperty("detail").GetString());
        Assert.False(doc.RootElement.TryGetProperty("isSuccessful", out _));
    }

    [Fact]
    public async Task Classifying_another_tenants_user_is_the_same_404_as_a_missing_one()
    {
        using var client = _host.Client(_seed.KindAdminToken, _seed.TenantId);

        var missing = await client.PostAsJsonAsync($"api/users/{Guid.NewGuid()}/account-kind", new { kind = "Human" });
        var foreign = await client.PostAsJsonAsync($"api/users/{_seed.Foreign.Id}/account-kind", new { kind = "Human" });

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());
    }

    // ── create with an explicit kind ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_a_kind_is_403_PERM_DENIED_for_a_creator_without_the_manage_key()
    {
        using var creator = _host.Client(_seed.CreatorToken, _seed.TenantId);
        var email = $"denied.{Guid.NewGuid():N}@acceptance.invalid";

        var response = await creator.PostAsJsonAsync("api/users", new { email, firstName = "De", lastName = "Nied", accountKind = "Human" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal(CreateUserCommandHandler.PermissionDeniedCode, doc.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
        // Nothing was created.
        var users = _host.Database.GetCollection<User>("users");
        Assert.Equal(0, await users.CountDocumentsAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Create_without_a_kind_is_Unknown_and_a_kind_admin_may_create_classified()
    {
        using var creator = _host.Client(_seed.CreatorToken, _seed.TenantId);
        using var kindAdmin = _host.Client(_seed.KindAdminToken, _seed.TenantId);
        var plainEmail = $"plain.{Guid.NewGuid():N}@acceptance.invalid";
        var serviceEmail = $"svc.{Guid.NewGuid():N}@acceptance.invalid";

        var plain = await creator.PostAsJsonAsync("api/users", new { email = plainEmail, firstName = "Pl", lastName = "Ain" });
        var classified = await kindAdmin.PostAsJsonAsync("api/users", new { email = serviceEmail, firstName = "Ser", lastName = "Vice", accountKind = "service" });

        Assert.Equal(HttpStatusCode.Created, plain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, classified.StatusCode);
        using (var doc = JsonDocument.Parse(await plain.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Unknown", doc.RootElement.GetProperty("data").GetProperty("accountKind").GetString());
        }

        using (var doc = JsonDocument.Parse(await classified.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Service", doc.RootElement.GetProperty("data").GetProperty("accountKind").GetString());
        }
    }

    private static Guid[] Ids(string lookupBody)
    {
        using var doc = JsonDocument.Parse(lookupBody);
        return doc.RootElement.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("userId").GetGuid()).ToArray();
    }
}
