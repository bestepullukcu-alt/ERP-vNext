using System.Diagnostics.Metrics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;
using Xunit;
using Host = Diten.Platform.Application.Tests.DocumentRepository.TrustedDocumentRepositoryConsumerHost;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 — the gate on the REAL HTTP host (AC-16 host part, AC-17, AC-19, AC-24, AC-26 unauthenticated
/// rows, AC-27). Every refusal: a bodied <c>Response</c> with its reason code, no command, no provider call, and the
/// record count of §8.1 "Ret başına tek yazar". Each named test is what goes red when its refusal branch is removed.
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerGateTests
{
    internal static readonly Guid Client = Guid.Parse("26202620-0000-4000-8000-0000000000c1");
    internal static readonly Guid TenantA = Guid.Parse("26202620-0000-4000-8000-0000000000a1");

    private static Task<Host> StartAsync() => Host.StartAsync(new HostOptions { Entries = [Host.Entry(Client)] });

    private static byte[] Pdf => Encoding.UTF8.GetBytes("%PDF contract");

    private static void AssertNoCommand(Host host)
    {
        Assert.Equal(0, host.Storage.StoreCalls);
        Assert.Empty(host.Options.Store.Rows);
    }

    private static AuditEvent SingleUnauthenticatedRecord(Host host, string reasonCode)
    {
        var write = Assert.Single(host.Audit.Writes);
        Assert.Equal(AuditTenantIds.PlatformSystemTenantId, write.TenantId);
        var record = InMemoryAuditOutbox.ToAuditEvent(write);
        Assert.Equal(AuditActorType.System, record.ActorType);
        Assert.Null(record.ActorId);
        Assert.Equal(TrustedDocumentRepositoryRefusalAudit.DeniedRequestType, record.RequestType);
        Assert.Equal(AuditOperation.PermissionDenied, record.Operation);
        Assert.Equal(reasonCode, record.Metadata["ReasonCode"]?.ToString());
        Assert.Equal(new[] { "Endpoint", "ReasonCode" }, TrustedDocumentRepositoryConsumerUploadTests.Keys(record));
        return record;
    }

    [Fact]
    public async Task No_authorization_header_is_401_with_one_platform_global_record()
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(null, Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        Assert.Equal(AuditOutcome.Denied, SingleUnauthenticatedRecord(host, DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated).Outcome);
    }

    public static TheoryData<string> InvalidTokens => new()
    {
        "foreign-signature",
        "expired",
        TrustedServiceTokenValidationExtensions.WorkflowRequiredAudience,
        TrustedServiceTokenValidationExtensions.RequiredAudience,
        TrustedServiceTokenValidationExtensions.ReferenceDataRequiredAudience
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task A_service_token_the_scheme_rejects_is_401_unauthenticated_and_platform_global(string kind)
    {
        await using var host = await StartAsync();
        using var foreignKey = RSA.Create(2048);
        var token = kind switch
        {
            "foreign-signature" => Host.ServiceToken(Client, TenantA, key: foreignKey),
            "expired" => Host.ServiceToken(Client, TenantA, issuedAtOffsetSeconds: -900),
            _ => Host.ServiceToken(Client, TenantA, audience: kind)
        };

        using var response = await host.UploadAsync(token, Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        SingleUnauthenticatedRecord(host, DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated);
    }

    [Theory]
    [InlineData("tenant-n-format")]
    [InlineData("no-jti")]
    public async Task A_claim_contract_breach_signed_with_the_right_key_is_401_not_403_and_writes_nothing_of_the_token(string breach)
    {
        await using var host = await StartAsync();
        var token = Host.ServiceToken(Client, TenantA, edit: claims =>
        {
            if (breach == "tenant-n-format")
            {
                claims.RemoveAll(c => c.Type == "tenant_id");
                claims.Add(new Claim("tenant_id", TenantA.ToString("N")));
            }
            else
            {
                claims.RemoveAll(c => c.Type == JwtRegisteredClaimNames.Jti);
            }
        });

        using var response = await host.UploadAsync(token, Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        SingleUnauthenticatedRecord(host, DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated);
        var payload = JsonSerializer.Serialize(host.Audit.Writes.Single().Payload);
        Assert.DoesNotContain(TenantA.ToString("N"), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TenantA.ToString("D"), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Client.ToString("D"), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(host.Audit.Writes, w => w.TenantId == TenantA);
    }

    [Theory]
    [InlineData("duplicate-authorization")]
    [InlineData("X-Tenant-Id")]
    [InlineData("X-Delegated-Authorization")]
    public async Task An_authority_header_the_contract_forbids_is_403_before_the_body_is_read(string header)
    {
        await using var host = await StartAsync();
        var token = Host.ServiceToken(Client, TenantA);

        using var response = await host.UploadAsync(token, Form.Valid(Pdf), request =>
        {
            if (header == "duplicate-authorization")
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(header, header == "X-Tenant-Id" ? TenantA.ToString() : "Bearer " + token);
            }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerForbidden, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        SingleUnauthenticatedRecord(host, DocumentRepositoryReasonCodes.TrustedConsumerForbidden);
    }

    [Fact]
    public async Task A_declared_body_over_the_request_limit_is_413_before_the_body_is_read()
    {
        await using var host = await StartAsync();
        var body = new ByteArrayContent(new byte[16]);
        body.Headers.ContentLength = TrustedDocumentRepositoryUploadLimits.MaxRequestBytes + 1; // 53 477 377

        using var response = await host.SendAsync(HttpMethod.Post, Host.BasePath, Host.ServiceToken(Client, TenantA),
            content: new DeclaredLengthContent(TrustedDocumentRepositoryUploadLimits.MaxRequestBytes + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerTooLarge, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        Assert.Equal(AuditOutcome.Failed, SingleUnauthenticatedRecord(host, DocumentRepositoryReasonCodes.TrustedConsumerTooLarge).Outcome);
    }

    [Fact]
    public async Task An_unconfigured_scheme_is_503_with_a_body_and_no_record_not_500()
    {
        await using var host = await Host.StartAsync(new HostOptions { ConfigureScheme = false, Entries = [Host.Entry(Client)] });

        using var response = await host.UploadAsync(Host.ServiceToken(Client, TenantA), Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnavailable, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        Assert.Empty(host.Audit.Writes);
    }

    [Fact]
    public async Task An_empty_closed_list_answers_a_valid_token_503_and_writes_no_record()
    {
        await using var host = await Host.StartAsync(new HostOptions());

        using var response = await host.GetAsync(Host.ServiceToken(Client, TenantA), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnavailable, await Host.ReasonCodeAsync(response));
        Assert.Empty(host.Audit.Writes);
    }

    [Fact]
    public async Task A_user_jwt_with_read_and_manage_is_401_and_its_record_names_no_person()
    {
        await using var host = await StartAsync();
        var user = Guid.NewGuid();
        var token = Host.UserToken(TenantA, user, "tenant_user",
            "platform.document-repository.read", "platform.document-repository.manage");

        using var response = await host.UploadAsync(token, Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        var record = SingleUnauthenticatedRecord(host, DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated);
        Assert.Null(record.ActorId);
        Assert.Null(record.ActorEmailMasked);
        Assert.Equal("s***m", record.ActorDisplayNameMasked);
        var payload = JsonSerializer.Serialize(host.Audit.Writes.Single().Payload);
        Assert.DoesNotContain(user.ToString(), payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dl2.person", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_client_not_on_the_closed_list_is_403_with_one_denied_record_under_the_token_tenant()
    {
        await using var host = await StartAsync();
        var stranger = Guid.NewGuid();

        using var response = await host.UploadAsync(Host.ServiceToken(stranger, TenantA), Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerForbidden, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        var write = Assert.Single(host.Audit.Writes);
        Assert.Equal(TenantA, write.TenantId);
        var record = InMemoryAuditOutbox.ToAuditEvent(write);
        Assert.Equal(AuditActorType.Service, record.ActorType);
        Assert.Equal(stranger, record.ActorId);
        Assert.Equal(AuditOutcome.Denied, record.Outcome);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerForbidden, record.Metadata["ReasonCode"]?.ToString());
    }

    [Theory]
    [InlineData("Templates")]
    [InlineData("CrmVisitEvidence")]
    public async Task A_known_scope_outside_the_entry_is_403_and_storage_is_not_called(string scope)
    {
        await using var host = await StartAsync();

        using var response = await host.UploadAsync(Host.ServiceToken(Client, TenantA), Form.Valid(Pdf, scope: scope));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerForbidden, await Host.ReasonCodeAsync(response));
        AssertNoCommand(host);
        var write = Assert.Single(host.Audit.Writes);
        Assert.Equal(TenantA, write.TenantId);
        Assert.Equal(AuditOutcome.Denied, InMemoryAuditOutbox.ToAuditEvent(write).Outcome);
    }

    [Fact]
    public async Task The_61st_unauthenticated_refusal_in_a_minute_is_answered_the_same_but_not_recorded_and_is_counted()
    {
        await using var host = await StartAsync();
        long unrecorded = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == TrustedDocumentRepositoryRefusalAudit.MeterName
                && instrument.Name == TrustedDocumentRepositoryRefusalAudit.UnrecordedCounterName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "reason" && Equals(tag.Value, TrustedDocumentRepositoryRefusalAudit.RateLimitedReason))
                {
                    Interlocked.Add(ref unrecorded, value);
                }
            }
        });
        listener.Start();

        for (var i = 0; i < 60; i++)
        {
            using var answered = await host.GetAsync(null, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.Unauthorized, answered.StatusCode);
        }

        Assert.Equal(60, host.Audit.Writes.Count);
        using var response = await host.GetAsync(null, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, await Host.ReasonCodeAsync(response));
        Assert.Equal(60, host.Audit.Writes.Count);
        Assert.Equal(1, Interlocked.Read(ref unrecorded));
        Assert.Equal(1, host.Logs.Count(LogLevel.Warning, "was not recorded"));
    }

    [Fact]
    public async Task Authentication_past_the_two_second_budget_is_504_without_storage_or_record()
    {
        await using var host = await Host.StartAsync(new HostOptions { SlowAuthentication = true, Entries = [Host.Entry(Client)] });
        var started = System.Diagnostics.Stopwatch.StartNew();

        using var response = await host.UploadAsync(Host.ServiceToken(Client, TenantA), Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerTimeout, await Host.ReasonCodeAsync(response));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5), $"answered after {started.Elapsed}");
        AssertNoCommand(host);
        Assert.Empty(host.Audit.Writes);
    }

    [Fact]
    public async Task A_refusal_whose_record_cannot_be_written_keeps_its_status()
    {
        await using var host = await StartAsync();
        host.Audit.Throws = true;

        using var response = await host.UploadAsync(null, Form.Valid(Pdf));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(DocumentRepositoryReasonCodes.TrustedConsumerUnauthenticated, await Host.ReasonCodeAsync(response));
    }

    public static TheoryData<string> BrokenClosedLists => new()
    {
        "module MOD-9999", "service Diten.Crm", "audience TRUSTED_WORKFLOW_CONSUMER", "scopes Templates",
        "two MOD-0217 entries", "same client twice", "nine entries", "general limit 52428799"
    };

    [Theory]
    [MemberData(nameof(BrokenClosedLists))]
    public async Task A_closed_list_outside_the_code_set_does_not_let_the_host_start(string fixture)
    {
        var valid = Host.Entry(Client);
        List<ConsumerEntry> entries = fixture switch
        {
            "module MOD-9999" => [valid with { ConsumerModule = "MOD-9999" }],
            "service Diten.Crm" => [valid with { ServiceName = "Diten.Crm" }],
            "audience TRUSTED_WORKFLOW_CONSUMER" => [valid with { Audience = TrustedServiceTokenValidationExtensions.WorkflowRequiredAudience }],
            "scopes Templates" => [valid with { Scopes = ["Templates"] }],
            "two MOD-0217 entries" => [valid, valid with { ClientId = Guid.NewGuid() }],
            "same client twice" => [valid, valid with { ConsumerModule = "MOD-0217" }],
            "nine entries" => Enumerable.Range(0, 9).Select(_ => valid with { ClientId = Guid.NewGuid() }).ToList(),
            _ => [valid]
        };
        var options = new HostOptions
        {
            Entries = entries,
            MaxFileSizeBytes = fixture == "general limit 52428799" ? 52_428_799 : 268_435_456
        };

        await Assert.ThrowsAsync<Microsoft.Extensions.Options.OptionsValidationException>(() => Host.StartAsync(options));
    }

    /// <summary>Declares a Content-Length without ever producing the body: the 413 must come from the header alone.</summary>
    private sealed class DeclaredLengthContent : HttpContent
    {
        public DeclaredLengthContent(long declared) => Headers.ContentLength = declared;

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            Task.Delay(Timeout.Infinite, new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);

        protected override bool TryComputeLength(out long length)
        {
            length = Headers.ContentLength ?? 0;
            return true;
        }
    }
}
