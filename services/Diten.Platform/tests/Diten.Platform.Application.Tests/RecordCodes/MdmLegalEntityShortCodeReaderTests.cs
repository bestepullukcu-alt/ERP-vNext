using System.Diagnostics;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Infrastructure.Services.Mdm;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §8.3 — the production short-code reader over a fake MDM: the pack's answer table row by row (AC-11, AC-13,
/// AC-36) and the 3 s budget (AC-26). The tenant on the wire is the one asked for; the user's token is forwarded.
/// </summary>
public sealed class MdmLegalEntityShortCodeReaderTests
{
    private static readonly Guid Tenant = Guid.Parse("40fc0002-0000-4000-8000-00000000a0a6");
    private static readonly Guid Company = Guid.Parse("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");

    [Theory]
    [InlineData("MYG")]
    [InlineData("GPL")]
    [InlineData("GMT")]
    [InlineData("GM")]
    public async Task AC11_an_exact_short_code_is_found(string code)
    {
        var mdm = new FakeMdm();
        mdm.Company(Company, code);

        var result = await Reader(mdm).ReadAsync(Tenant, Company);

        Assert.Equal((LegalEntityShortCodeOutcome.Found, code), (result.Outcome, result.Code));
    }

    [Theory]
    [InlineData("GMG-CH")]
    [InlineData("my")]
    [InlineData("M")]
    [InlineData("ABCDEF")]
    [InlineData("MYĞ")]
    [InlineData("")]
    [InlineData(" MYG")]
    [InlineData("MYG ")]
    [InlineData("myg")]
    public async Task AC11_a_short_code_that_does_not_match_exactly_is_invalid_never_normalised(string code)
    {
        var mdm = new FakeMdm();
        mdm.Company(Company, code);

        var result = await Reader(mdm).ReadAsync(Tenant, Company);

        Assert.Equal(LegalEntityShortCodeOutcome.ShortCodeInvalid, result.Outcome);
        Assert.Null(result.Code);
    }

    [Theory]
    [InlineData("\"APPROVED\"", LegalEntityShortCodeOutcome.Found)]
    [InlineData("\"DRAFT\"", LegalEntityShortCodeOutcome.NotEligible)]
    [InlineData("\"SUBMITTED\"", LegalEntityShortCodeOutcome.NotEligible)]
    [InlineData("\"REJECTED\"", LegalEntityShortCodeOutcome.NotEligible)]
    [InlineData("null", LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData("\"\"", LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData("\"approved\"", LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData("\"ARCHIVED\"", LegalEntityShortCodeOutcome.Unavailable)]
    public async Task AC36_a_reported_approval_status_is_judged_and_an_absent_one_is_left_to_the_consumer(string approval, LegalEntityShortCodeOutcome expected)
    {
        var mdm = new FakeMdm();
        mdm.Company(Company, "MYG", approval);
        var absent = new FakeMdm();
        absent.Company(Company, "MYG");

        Assert.Equal(expected, (await Reader(mdm).ReadAsync(Tenant, Company)).Outcome);
        Assert.Equal(LegalEntityShortCodeOutcome.Found, (await Reader(absent).ReadAsync(Tenant, Company)).Outcome);
    }

    [Theory]
    [InlineData(404, LegalEntityShortCodeOutcome.NotFound)]
    [InlineData(403, LegalEntityShortCodeOutcome.ReadPermissionMissing)]
    [InlineData(400, LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData(401, LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData(500, LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData(502, LegalEntityShortCodeOutcome.Unavailable)]
    [InlineData(503, LegalEntityShortCodeOutcome.Unavailable)]
    public async Task AC13_MDM_statuses_map_to_the_pack_table(int status, LegalEntityShortCodeOutcome expected)
    {
        var mdm = new FakeMdm();
        mdm.Answer(Company, status);

        Assert.Equal(expected, (await Reader(mdm).ReadAsync(Tenant, Company)).Outcome);
    }

    public static TheoryData<string> ContradictoryBodies => new()
    {
        "not json",
        "{}",
        "{\"data\":null}",
        "{\"data\":{\"legalEntityId\":\"" + Guid.NewGuid() + "\",\"code\":\"MYG\",\"lifecycleState\":\"ACTIVE\",\"referenceable\":true}}",
        "{\"data\":{\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"code\":\"MYG\",\"lifecycleState\":\"ACTIVE\",\"referenceable\":false}}",
        "{\"data\":{\"legalEntityId\":\"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7\",\"code\":\"MYG\",\"lifecycleState\":\"INACTIVE\",\"referenceable\":true}}"
    };

    [Theory]
    [MemberData(nameof(ContradictoryBodies))]
    public async Task AC13_a_200_that_is_unreadable_or_not_an_active_referenceable_match_is_unavailable(string body)
    {
        var mdm = new FakeMdm();
        mdm.Answer(Company, 200, body);

        Assert.Equal(LegalEntityShortCodeOutcome.Unavailable, (await Reader(mdm).ReadAsync(Tenant, Company)).Outcome);
    }

    [Fact]
    public async Task AC13_a_connection_failure_is_unavailable()
    {
        var mdm = new FakeMdm();
        mdm.Throw(Company);

        Assert.Equal(LegalEntityShortCodeOutcome.Unavailable, (await Reader(mdm).ReadAsync(Tenant, Company)).Outcome);
    }

    [Fact]
    public async Task AC26_an_MDM_slower_than_3_s_is_unavailable_within_the_budget()
    {
        var mdm = new FakeMdm { Delay = TimeSpan.FromSeconds(10) };
        mdm.Company(Company, "MYG");
        var clock = Stopwatch.StartNew();

        var result = await Reader(mdm).ReadAsync(Tenant, Company);

        Assert.Equal(LegalEntityShortCodeOutcome.Unavailable, result.Outcome);
        Assert.InRange(clock.Elapsed, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(4.5));
    }

    [Fact]
    public async Task AC12_the_wire_carries_the_asked_tenant_and_the_forwarded_token()
    {
        var mdm = new FakeMdm();
        mdm.Company(Company, "MYG");

        await Reader(mdm, "Bearer user-token").ReadAsync(Tenant, Company);

        var request = Assert.Single(mdm.Requests);
        Assert.Equal((Company, Tenant.ToString("D"), "Bearer user-token"), (request.LegalEntityId, request.Tenant, request.Authorization));
    }

    private static MdmLegalEntityShortCodeReader Reader(FakeMdm mdm, string? authorization = null)
    {
        var http = new DefaultHttpContext();
        if (authorization is not null)
        {
            http.Request.Headers.Authorization = authorization;
        }

        return new MdmLegalEntityShortCodeReader(
            new HttpClient(mdm) { Timeout = Timeout.InfiniteTimeSpan },
            Microsoft.Extensions.Options.Options.Create(new MdmServiceOptions { BaseUrl = "http://mdm.reader.test/" }),
            new HttpContextAccessor { HttpContext = http });
    }
}
