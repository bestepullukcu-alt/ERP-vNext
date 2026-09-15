using System.Net;
using System.Text.Json;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Tests.Testing;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-INFRA-AUTH-DISPLAY-LABEL-01 — HTTP round trips through the real Api (WebApplicationFactory) against the
/// shared <see cref="AccountKindAcceptance.AuthTestHost"/>, extended with four disposable display-label subjects
/// (<see cref="AccountKindAcceptance.SeedDisplayLabelSubjectsAsync"/>). The subjects and actors from
/// <see cref="AccountKindAcceptance.Seed"/> are reused as-is; nothing here writes to the DefaultTenant.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UserDisplayLabelEndpointTests : IClassFixture<AccountKindAcceptance.AuthTestHost>, IAsyncLifetime
{
    private readonly AccountKindAcceptance.AuthTestHost _host;
    private readonly AccountKindAcceptance.Seed _seed;
    private AccountKindAcceptance.DisplayLabelSubjects _subjects = null!;

    public UserDisplayLabelEndpointTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
        _seed = host.Seeded;
    }

    public async Task InitializeAsync()
    {
        _subjects = await AccountKindAcceptance.SeedDisplayLabelSubjectsAsync(_host);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // T1 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Pmo_reads_a_named_subjects_label_in_its_own_tenant()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{_seed.Human.Id}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("isSuccessful").GetBoolean());
        Assert.Equal(200, doc.RootElement.GetProperty("statusCode").GetInt32());
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(_seed.Human.Id, data.GetProperty("userId").GetGuid());
        Assert.Equal("Named", data.GetProperty("labelState").GetString());
        Assert.Equal("Zehra Yıldız", data.GetProperty("displayLabel").GetString());
    }

    // T2 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("Unnamed")]
    [InlineData("Whitespace")]
    public async Task Blank_or_whitespace_only_names_are_unnamed_with_a_null_label(string subject)
    {
        var user = subject == "Unnamed" ? _subjects.Unnamed : _subjects.Whitespace;
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{user.Id}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("Unnamed", data.GetProperty("labelState").GetString());
        Assert.True(data.TryGetProperty("displayLabel", out var label), "displayLabel key must be present");
        Assert.Equal(JsonValueKind.Null, label.ValueKind);
    }

    // T3 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task An_email_shaped_username_never_leaks_and_is_still_unnamed()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{_subjects.EmailUserName.Id}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Unnamed", doc.RootElement.GetProperty("data").GetProperty("labelState").GetString());
        Assert.DoesNotContain("@", body);
        Assert.DoesNotContain("displaylabel-emailusername", body);
    }

    // T4 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task A_very_long_name_is_never_truncated()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{_subjects.LongName.Id}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal("Named", data.GetProperty("labelState").GetString());
        Assert.Equal(301, data.GetProperty("displayLabel").GetString()!.Length);
    }

    // T5 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task An_inactive_account_still_answers_200_named()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{_seed.Passive.Id}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Named", doc.RootElement.GetProperty("data").GetProperty("labelState").GetString());
    }

    // T6 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Missing_and_foreign_tenant_404s_match_the_account_assertion_endpoints_bytes()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);
        var missingId = Guid.NewGuid();

        var missingLabel = await client.GetAsync($"api/users/{missingId}/display-label");
        var foreignLabel = await client.GetAsync($"api/users/{_seed.Foreign.Id}/display-label");
        var missingAssertion = await client.GetAsync($"api/users/{missingId}/account-assertion");
        var foreignAssertion = await client.GetAsync($"api/users/{_seed.Foreign.Id}/account-assertion");

        Assert.Equal(HttpStatusCode.NotFound, missingLabel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignLabel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingAssertion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignAssertion.StatusCode);

        var missingLabelBody = await missingLabel.Content.ReadAsStringAsync();
        var foreignLabelBody = await foreignLabel.Content.ReadAsStringAsync();
        var missingAssertionBody = await missingAssertion.Content.ReadAsStringAsync();
        var foreignAssertionBody = await foreignAssertion.Content.ReadAsStringAsync();

        Assert.Equal(missingLabelBody, foreignLabelBody);
        Assert.Equal(missingLabelBody, missingAssertionBody);
        Assert.Equal(missingLabelBody, foreignAssertionBody);
    }

    // T7 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Missing_or_garbage_token_is_401_no_permission_is_403_creator_is_403_pmo_is_200()
    {
        using var anonymous = _host.Client(null, _seed.TenantId);
        using var garbage = _host.Client("not.a.jwt", _seed.TenantId);
        using var noPermission = _host.Client(_seed.NoPermissionToken, _seed.TenantId);
        using var creator = _host.Client(_seed.CreatorToken, _seed.TenantId);
        using var pmo = _host.Client(_seed.PmoToken, _seed.TenantId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"api/users/{_seed.Human.Id}/display-label")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await garbage.GetAsync($"api/users/{_seed.Human.Id}/display-label")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await noPermission.GetAsync($"api/users/{_seed.Human.Id}/display-label")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.GetAsync($"api/users/{_seed.Human.Id}/display-label")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await pmo.GetAsync($"api/users/{_seed.Human.Id}/display-label")).StatusCode);
    }

    // T8 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("Foreign")]
    [InlineData("Human")]
    public async Task A_contradicting_tenant_header_is_refused_before_the_handler_runs_and_discloses_nothing(string target)
    {
        var targetId = target == "Foreign" ? _seed.Foreign.Id : _seed.Human.Id;
        using var client = _host.Client(_seed.PmoToken, _seed.ForeignTenantId);

        var response = await client.GetAsync($"api/users/{targetId}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Tenant mismatch", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal("JWT tenant and 'X-Tenant-Id' must match.", doc.RootElement.GetProperty("detail").GetString());
        Assert.False(doc.RootElement.TryGetProperty("data", out _));

        Assert.DoesNotContain(targetId.ToString(), body);
        Assert.DoesNotContain("Fatma", body);
        Assert.DoesNotContain("Öztürk", body);
        Assert.DoesNotContain("Zehra", body);
        Assert.DoesNotContain("Yıldız", body);
        Assert.DoesNotContain("displayLabel", body);
        Assert.DoesNotContain("labelState", body);
    }

    // T9 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task LabelState_is_always_a_json_string()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{_seed.Human.Id}/display-label");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("data").GetProperty("labelState").ValueKind);
    }

    // T10 ───────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task An_empty_guid_is_the_handlers_400_in_the_response_envelope()
    {
        using var client = _host.Client(_seed.PmoToken, _seed.TenantId);

        var response = await client.GetAsync($"api/users/{Guid.Empty}/display-label");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.GetProperty("isSuccessful").GetBoolean());
        var errors = doc.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(new[] { "UserId is required." }, errors);
    }

    // T11 ───────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void UserDisplayLabelDto_carries_exactly_three_public_properties()
    {
        var properties = typeof(UserDisplayLabelDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(new[] { "DisplayLabel", "LabelState", "UserId" }, properties);
    }

    // T12 ───────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void AccountAssertionDto_property_set_is_unchanged()
    {
        var properties = typeof(AccountAssertionDto).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            new[] { "Active", "AccountKind", "AssertedAt", "UserId", "UserUpdatedAt" }.OrderBy(n => n).ToArray(),
            properties);
    }
}
