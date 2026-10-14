using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3E · W3E-BE-b (§2.1, §2.5) on the PRODUCTION start handler: the device declaration is RECORDED in every mode,
/// FLAGGED only in <c>antiFraudMode=flag</c> (<c>mock_location</c>, <c>device_integrity_failed</c>, <c>device_unverified</c>
/// for the Web / no verdict / verdict unavailable), and NEVER refuses a start; the integrity token never reaches the
/// handler (byte count at the controller), the database or the DTO; a malformed declaration is a plain 400; the policy
/// field (PUT, invalid "block", history row, an older document) and the contract vocabularies.
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    private static StartVisitExecutionCommand StartWith(PlanAtom plan, VisitDeviceEvidenceInput? device)
        => StartAt(plan) with { Device = device };

    private void FlagMode() => _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, AntiFraudMode = VisitAntiFraudModes.Flag, Version = 1 };

    private static VisitDeviceEvidenceInput Device(string? platform, string? verdict = null, bool? mock = null, string? provider = null, int? tokenBytes = null, string? app = null)
        => new(platform, app, mock, provider, verdict, tokenBytes);

    private static readonly string[] DeviceFlags = [VisitExecutionFlags.MockLocation, VisitExecutionFlags.DeviceIntegrityFailed, VisitExecutionFlags.DeviceUnverified];

    [Theory]
    // platform,    verdict,       mock,  expected flags in "flag" mode
    [InlineData("web", null, null, new[] { "device_unverified" })]
    [InlineData("web", "passed", null, new[] { "device_unverified" })]          // a browser cannot vouch, whatever it says
    [InlineData("android", "passed", false, new string[0])]
    [InlineData("android", "passed", true, new[] { "mock_location" })]
    [InlineData("ios", "failed", null, new[] { "device_integrity_failed" })]
    [InlineData("android", "unavailable", null, new[] { "device_unverified" })]
    [InlineData("android", null, null, new[] { "device_unverified" })]         // no integrity declaration
    [InlineData(null, null, null, new[] { "device_unverified" })]              // nothing declared at all
    [InlineData("android", "failed", true, new[] { "mock_location", "device_integrity_failed" })]
    public async Task In_flag_mode_the_device_flags_follow_the_three_rules_and_the_start_is_never_refused(
        string? platform, string? verdict, bool? mock, string[] expected)
    {
        FlagMode();
        var res = await Starter().Handle(StartWith(Seed(), platform is null && verdict is null && mock is null ? null : Device(platform, verdict, mock)), default);

        Assert.Equal(201, res.StatusCode);                                        // never a refusal (K-W3E-3)
        Assert.Equal(expected, res.Data!.Flags.Where(DeviceFlags.Contains).ToArray());
        Assert.False(res.Data.Device!.Verified);                                  // W3E-ATTEST verifies, not this package
        Assert.Equal(platform, res.Data.Device.Platform);
    }

    [Fact]
    public async Task In_off_mode_the_device_is_recorded_without_any_flag_and_the_w3_start_is_unchanged()
    {
        // the default policy (no document) is off
        var res = await Starter().Handle(StartWith(Seed(), Device("android", "failed", true, "play_integrity", 120, "3.2.1")), default);
        Assert.Equal(201, res.StatusCode);
        Assert.Empty(res.Data!.Flags.Where(DeviceFlags.Contains));
        var stored = _executions.Items.Single().Device!;
        Assert.Equal(("android", "3.2.1", true, "play_integrity", "failed", true, false),
            (stored.Platform, stored.AppVersion, stored.MockLocation, stored.IntegrityProvider, stored.IntegrityVerdict, stored.TokenProvided, stored.Verified));

        // a start without any declaration still records an (empty) device in off mode and flags nothing
        var plain = await Starter().Handle(StartAt(Seed()), default);
        Assert.Equal(201, plain.StatusCode);
        Assert.Empty(plain.Data!.Flags);
        Assert.Null(plain.Data.Device!.Platform);
    }

    [Fact]
    public async Task The_integrity_token_never_reaches_the_handler_the_database_or_the_answer()
    {
        FlagMode();
        const string token = "SECRET-INTEGRITY-TOKEN-eyJhbGciOiJFUzI1NiJ9.payload.signature";
        var device = VisitExecutionController.DeviceOf("android", "4.0", false, "play_integrity", "passed", token);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(token), device!.IntegrityTokenBytes);
        Assert.DoesNotContain(token, device.ToString());                          // only its size travels

        var res = await Starter().Handle(StartWith(Seed(), device), default);
        Assert.Equal(201, res.StatusCode);
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var bson = _executions.Items.Single().ToBsonDocument().ToJson();
        Assert.DoesNotContain("SECRET-INTEGRITY-TOKEN", bson);                    // a document scan: not stored
        Assert.Contains("\"TokenProvided\" : true", bson);
        Assert.DoesNotContain("SECRET", System.Text.Json.JsonSerializer.Serialize(res.Data));
        Assert.True(res.Data!.Device!.TokenProvided);
        Assert.Null(VisitExecutionController.DeviceOf(null, null, null, null, null, null)); // nothing declared ⇒ no input
    }

    [Fact]
    public async Task A_malformed_declaration_is_a_plain_400_and_nothing_is_written()
    {
        FlagMode();
        foreach (var bad in new[]
                 {
                     Device("android", tokenBytes: VisitExecutionLimits.MaxIntegrityTokenBytes + 1),   // > 8 KB
                     Device("blackberry"), Device("android", "maybe"), Device("android", provider: "safetynet"),
                     Device("android", app: new string('9', VisitExecutionLimits.MaxAppVersionLength + 1))
                 })
        {
            AssertCode(await Starter().Handle(StartWith(Seed(), bad), default), 400, VisitExecutionErrorCodes.CaptureInvalid);
        }

        Assert.Empty(_executions.Items);
        Assert.Equal(201, (await Starter().Handle(StartWith(Seed(), Device("android", tokenBytes: VisitExecutionLimits.MaxIntegrityTokenBytes)), default)).StatusCode);
    }

    [Fact]
    public async Task The_anti_fraud_mode_is_a_policy_field_with_history_and_an_older_document_reads_off()
    {
        var handler = new UpdateVisitEvidencePolicyHandler(TenantCtx(), new NullActorContext(), _policies);
        var saved = await handler.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput(
            null, null, null, null, null, null, null, null, null, AntiFraudMode: "flag")), default);
        Assert.Equal(200, saved.StatusCode);
        Assert.Equal(VisitAntiFraudModes.Flag, saved.Data!.AntiFraudMode);
        Assert.Equal(VisitAntiFraudModes.Flag, _policies.Stored!.AntiFraudMode);

        // the history renders the field like the others
        Assert.Contains(("antiFraudMode", "flag"), VisitEvidencePolicyResolver.Values(_policies.Stored));
        Assert.Equal(VisitAntiFraudModes.Flag, VisitEvidencePolicyResolver.Copy(_policies.Stored).AntiFraudMode);

        // "block" comes with W3E-ATTEST: today an invalid value
        AssertCode(await handler.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput(
            null, null, null, null, null, null, null, null, _policies.Stored.Version, AntiFraudMode: "block")), default), 400, VisitExecutionErrorCodes.PolicyInvalid);

        // an older document (no AntiFraudMode element) reads the default, nothing written back
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var doc = new VisitEvidencePolicy { TenantId = Tenant }.ToBsonDocument();
        doc.Remove("AntiFraudMode");
        Assert.Equal(VisitAntiFraudModes.Off, BsonSerializer.Deserialize<VisitEvidencePolicy>(doc).AntiFraudMode);
        Assert.Equal(VisitAntiFraudModes.Off, VisitEvidencePolicyDefaults.AntiFraudMode);
    }

    [Fact]
    public async Task The_contract_publishes_the_device_vocabularies_and_the_token_bound()
    {
        FlagMode();
        var contract = (await new GetVisitExecutionContractHandler(TenantCtx(), _policies, RepCaller(), clock: _clock)
            .Handle(new GetVisitExecutionContractQuery(), default)).Data!;
        Assert.Equal(VisitAntiFraudModes.All, contract.AntiFraudModes);
        Assert.Equal(new[] { "web", "android", "ios" }, contract.ClientPlatforms);
        Assert.Equal(new[] { "passed", "failed", "unavailable" }, contract.IntegrityVerdicts);
        Assert.Equal(new[] { "play_integrity", "app_attest" }, contract.IntegrityProviders);
        Assert.Equal(8192, contract.MaxIntegrityTokenBytes);
        Assert.Equal("flag", contract.Policy.AntiFraudMode);
        foreach (var flag in DeviceFlags) Assert.Contains(flag, contract.FlagCodes);
    }
}
