using System.Reflection;
using System.Text.RegularExpressions;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitQr;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3E · W3E-BE-a (Acceptance) on the PRODUCTION signer, verifier, start handler and QR handlers, with the W3
/// fixture (today = 2026-10-15 UTC, the rep's plan at an institution ~111 m away): the signature (5), the version (3), the
/// §2.4 table (13 rows, remote included), the parent chain (3), the qr_optional exception, concurrency, bulk, keys,
/// tenant isolation, no key, backward compatibility. The test injects its OWN keys — no configured secret is read.
/// </summary>
public sealed partial class VisitExecutionW3Tests
{
    private static readonly Guid OtherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly FakeInstitutionQrCodeRepository _qrCodes = new();
    private readonly FakeInstitutionQrEventRepository _qrEvents = new();
    private TestQrKeys _qrKeys = TestQrKeys.Active();

    private VisitQrService QrService(TestQrKeys? keys = null, ITenantContext? tenant = null)
        => new(tenant ?? TenantCtx(), new NullActorContext(), _accounts, _qrCodes, _qrEvents, keys ?? _qrKeys, _clock);

    private StartVisitExecutionHandler QrStarter()
        => new(TenantCtx(), new NullActorContext(), RepCaller(), _plans, _reports, _executions, _policies, _accounts, _store, _clock,
            new VisitEvidencePolicyResolver(_policies, _exceptionStore), new VisitQrVerifier(_qrCodes, _accounts, _qrKeys));

    private static StartVisitExecutionCommand StartQr(PlanAtom plan, string? payload, string kind = "in_person", double? lat = NearLat, VisitEvidenceFile? photo = null)
        => new(plan.Id, kind, lat, AccLng, 20, null, photo, null, QrPayload: payload, QrScannedAt: "2026-10-15T08:59:30Z");

    private void QrMode(string mode, string photo = VisitPhotoModes.Optional)
        => _policies.Stored = new VisitEvidencePolicy { TenantId = Tenant, QrMode = mode, PhotoMode = photo, Version = 1 };

    private Account AccountOf(PlanAtom plan) => _accounts.Items.Single(a => a.Id == plan.AccountId);

    private Account NewAccount(Guid? parent = null, Guid? tenant = null)
    {
        var a = new Account { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, AccountName = "Kurum " + (_accounts.Items.Count + 1), ParentAccountId = parent, Latitude = AccLat, Longitude = AccLng };
        _accounts.Items.Add(a);
        return a;
    }

    private async Task<Response<VisitQrAccountDto>> IssueAsync(Guid accountId, int? expected = null, TestQrKeys? keys = null)
        => await new IssueVisitQrHandler(QrService(keys)).Handle(new IssueVisitQrCommand(accountId, new VisitQrIssueInput(expected, null)), default);

    private async Task<string> PayloadAsync(Guid accountId)
        => (await new GetVisitQrPayloadHandler(QrService()).Handle(new GetVisitQrPayloadCommand(accountId), default)).Data!.Payload;

    private static string Tamper(string payload, int index)
    {
        var c = payload[index];
        return payload[..index] + (c == 'A' ? 'B' : 'A') + payload[(index + 1)..];
    }

    // ═══ the signature (5) ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Qr_signer_a_correct_payload_verifies_and_has_the_fixed_shape()
    {
        var keys = TestQrKeys.Active().Keys;
        var account = Guid.NewGuid();
        var payload = VisitQrSigner.Sign(keys, Tenant, account, 3);

        Assert.Matches(new Regex("^DQR1\\.[A-Za-z0-9_-]{22}\\.3\\.k1\\.[A-Za-z0-9_-]{43}$"), payload);
        Assert.True(payload.Length <= VisitQrLimits.MaxPayloadLength);
        var verified = VisitQrSigner.Verify(keys, Tenant, payload)!;
        Assert.Equal((account, 3, "k1"), (verified.AccountId, verified.Version, verified.KeyId));
        Assert.Equal(payload, VisitQrSigner.Sign(keys, Tenant, account, 3)); // deterministic: a reprint is the same text
    }

    [Fact]
    public void Qr_signer_one_changed_character_anywhere_is_invalid()
    {
        var keys = TestQrKeys.Active().Keys;
        var payload = VisitQrSigner.Sign(keys, Tenant, Guid.NewGuid(), 1);
        for (var i = 0; i < payload.Length; i++)
        {
            if (payload[i] == '.') continue;
            Assert.Null(VisitQrSigner.Verify(keys, Tenant, Tamper(payload, i)));
        }

        Assert.Null(VisitQrSigner.Verify(keys, Tenant, payload + "A"));
        Assert.Null(VisitQrSigner.Verify(keys, Tenant, new string('A', 201)));
        Assert.Null(VisitQrSigner.Verify(keys, Tenant, "https://example.com/qr"));
    }

    [Fact]
    public void Qr_signer_another_tenants_payload_is_invalid_and_names_no_tenant()
    {
        var keys = TestQrKeys.Active().Keys;
        var account = Guid.NewGuid();
        var foreign = VisitQrSigner.Sign(keys, OtherTenant, account, 1);
        Assert.Null(VisitQrSigner.Verify(keys, Tenant, foreign));
        Assert.DoesNotContain(OtherTenant.ToString("N")[..8], foreign, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(VisitQrSigner.DeriveTenantKey(TestQrKeys.K1, Tenant), VisitQrSigner.DeriveTenantKey(TestQrKeys.K1, OtherTenant));
    }

    [Fact]
    public void Qr_signer_an_unknown_key_id_is_invalid()
    {
        var withK2 = TestQrKeys.Active("k2", withK2: true).Keys;
        var payload = VisitQrSigner.Sign(withK2, Tenant, Guid.NewGuid(), 1); // signed with k2
        Assert.Null(VisitQrSigner.Verify(TestQrKeys.Active().Keys, Tenant, payload)); // a ring without k2
        Assert.Null(VisitQrSigner.Verify(TestQrKeys.Active().Keys, Tenant, payload.Replace(".k2.", ".zz.")));
    }

    [Fact]
    public void Qr_signer_an_old_key_id_still_verifies_after_rotation()
    {
        var before = TestQrKeys.Active("k1").Keys;
        var account = Guid.NewGuid();
        var old = VisitQrSigner.Sign(before, Tenant, account, 1);
        var rotated = TestQrKeys.Active("k2", withK2: true).Keys; // k2 active, k1 kept for verification
        Assert.NotNull(VisitQrSigner.Verify(rotated, Tenant, old));
        Assert.Contains(".k2.", VisitQrSigner.Sign(rotated, Tenant, account, 2));
    }

    // ═══ the version (3) ════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Qr_regenerate_revokes_the_old_payload_and_the_new_one_is_ok()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        Assert.Equal(201, (await IssueAsync(plan.AccountId!.Value)).StatusCode);
        var v1 = await PayloadAsync(plan.AccountId!.Value);
        var regen = await IssueAsync(plan.AccountId!.Value, expected: 1);
        Assert.Equal((201, 2, InstitutionQrCodeStatuses.Active), (regen.StatusCode, regen.Data!.Version, regen.Data.Status));
        var v2 = await PayloadAsync(plan.AccountId!.Value);
        Assert.NotEqual(v1, v2);

        AssertCode(await QrStarter().Handle(StartQr(plan, v1), default), 409, VisitExecutionErrorCodes.QrRevoked);
        var ok = await QrStarter().Handle(StartQr(plan, v2), default);
        Assert.Equal(201, ok.StatusCode);
        Assert.Equal((VisitQrScanStatuses.Ok, 2), (ok.Data!.Qr!.Status, ok.Data.Qr.Version));
        Assert.Equal(new[] { InstitutionQrEventKinds.Issued, InstitutionQrEventKinds.Printed, InstitutionQrEventKinds.Regenerated, InstitutionQrEventKinds.Printed },
            _qrEvents.Items.Select(e => e.Kind));
        Assert.Equal("regenerated", _qrCodes.Items.Single(c => c.CodeVersion == 1).RevokeReason);
    }

    [Fact]
    public async Task Qr_revoke_makes_the_payload_revoked_and_needs_a_reason_and_the_version()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        await IssueAsync(plan.AccountId!.Value);
        var payload = await PayloadAsync(plan.AccountId!.Value);
        var service = QrService();
        AssertCode(await new RevokeVisitQrHandler(service).Handle(new RevokeVisitQrCommand(plan.AccountId!.Value, new VisitQrRevokeInput(1, " ")), default),
            400, VisitExecutionErrorCodes.QrRequestInvalid);
        AssertCode(await new RevokeVisitQrHandler(service).Handle(new RevokeVisitQrCommand(plan.AccountId!.Value, new VisitQrRevokeInput(1, new string('x', 501))), default),
            400, VisitExecutionErrorCodes.QrRequestInvalid);
        AssertCode(await new RevokeVisitQrHandler(service).Handle(new RevokeVisitQrCommand(plan.AccountId!.Value, new VisitQrRevokeInput(7, "lost")), default),
            409, VisitExecutionErrorCodes.ConcurrencyConflict);
        var revoked = await new RevokeVisitQrHandler(service).Handle(new RevokeVisitQrCommand(plan.AccountId!.Value, new VisitQrRevokeInput(1, "Sticker stolen")), default);
        Assert.Equal((200, InstitutionQrCodeStatuses.Revoked, "Sticker stolen"), (revoked.StatusCode, revoked.Data!.Status, revoked.Data.RevokeReason));

        // the institution now has NO active code: a required start would be not_issued; the old sticker is revoked
        _policies.Stored!.QrMode = VisitQrModes.Optional;
        var res = await QrStarter().Handle(StartQr(plan, payload), default);
        Assert.Equal(VisitQrScanStatuses.Revoked, res.Data!.Qr!.Status);
        Assert.Contains(VisitExecutionFlags.QrRevoked, res.Data.Flags);
        AssertCode(await new GetVisitQrPayloadHandler(service).Handle(new GetVisitQrPayloadCommand(plan.AccountId!.Value), default), 409, VisitExecutionErrorCodes.QrNotActive);
    }

    [Fact]
    public async Task Qr_reprint_gives_the_same_payload_and_writes_a_printed_event_each_time()
    {
        var account = NewAccount();
        await IssueAsync(account.Id);
        var a = await PayloadAsync(account.Id);
        var b = await PayloadAsync(account.Id);
        Assert.Equal(a, b);
        Assert.Equal(2, _qrEvents.Items.Count(e => e.Kind == InstitutionQrEventKinds.Printed));
        var state = (await new GetVisitQrAccountHandler(QrService()).Handle(new GetVisitQrAccountQuery(account.Id), default)).Data!;
        Assert.Equal((InstitutionQrCodeStatuses.Active, 1), (state.Status, state.Version));
        var json = System.Text.Json.JsonSerializer.Serialize(state);
        Assert.DoesNotContain("DQR1", json); // the state read never carries a payload
    }

    // ═══ the §2.4 table (13 rows) ═══════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(VisitQrModes.Off, "off", 201, null, VisitQrScanStatuses.NotApplicable)]
    [InlineData(VisitQrModes.Required, "remote", 201, null, VisitQrScanStatuses.NotApplicable)]
    [InlineData(VisitQrModes.Required, "ok", 201, null, VisitQrScanStatuses.Ok)]
    [InlineData(VisitQrModes.Required, "missing", 400, VisitExecutionErrorCodes.QrRequired, null)]
    [InlineData(VisitQrModes.Required, "invalid", 400, VisitExecutionErrorCodes.QrInvalid, null)]
    [InlineData(VisitQrModes.Required, "wrong", 409, VisitExecutionErrorCodes.QrWrongAccount, null)]
    [InlineData(VisitQrModes.Required, "revoked", 409, VisitExecutionErrorCodes.QrRevoked, null)]
    [InlineData(VisitQrModes.Required, "not_issued", 201, VisitExecutionFlags.QrNotIssued, VisitQrScanStatuses.NotIssued)]
    [InlineData(VisitQrModes.Required, "unverifiable", 503, VisitExecutionErrorCodes.QrSigningUnavailable, null)]
    [InlineData(VisitQrModes.Optional, "ok", 201, null, VisitQrScanStatuses.Ok)]
    [InlineData(VisitQrModes.Optional, "missing", 201, null, VisitQrScanStatuses.Missing)]
    [InlineData(VisitQrModes.Optional, "invalid", 201, VisitExecutionFlags.QrInvalid, VisitQrScanStatuses.Invalid)]
    [InlineData(VisitQrModes.Optional, "wrong", 201, VisitExecutionFlags.QrWrongAccount, VisitQrScanStatuses.WrongAccount)]
    [InlineData(VisitQrModes.Optional, "revoked", 201, VisitExecutionFlags.QrRevoked, VisitQrScanStatuses.Revoked)]
    [InlineData(VisitQrModes.Optional, "not_issued", 201, null, VisitQrScanStatuses.NotIssued)]
    [InlineData(VisitQrModes.Optional, "unverifiable", 201, VisitExecutionFlags.QrUnverifiable, VisitQrScanStatuses.Unverifiable)]
    public async Task Qr_start_table_row(string mode, string scenario, int status, string? codeOrFlag, string? qrStatus)
    {
        QrMode(mode);
        var plan = Seed();
        var account = plan.AccountId!.Value;
        string? payload = null;
        var kind = "in_person";
        switch (scenario)
        {
            case "off":
                payload = "not-even-a-payload";
                break;
            case "remote":
                kind = "remote";
                break;
            case "ok":
                await IssueAsync(account);
                payload = await PayloadAsync(account);
                break;
            case "missing":
                await IssueAsync(account);
                break;
            case "invalid":
                await IssueAsync(account);
                payload = Tamper(await PayloadAsync(account), 60);
                break;
            case "wrong":
                await IssueAsync(account);
                var other = NewAccount();
                await IssueAsync(other.Id);
                payload = await PayloadAsync(other.Id);
                break;
            case "revoked":
                await IssueAsync(account);
                payload = await PayloadAsync(account);
                await IssueAsync(account, expected: 1);
                break;
            case "not_issued":
                break;
            case "unverifiable":
                await IssueAsync(account);
                payload = await PayloadAsync(account);
                _qrKeys.Keys = VisitQrKeys.None; // the key went away (outside Development: no fallback)
                break;
        }

        var res = await QrStarter().Handle(StartQr(plan, payload, kind), default);
        Assert.Equal(status, res.StatusCode);
        if (status != 201)
        {
            Assert.Contains(codeOrFlag!, res.Errors!);
            Assert.Empty(_executions.Items); // a refused start writes nothing
            if (scenario == "wrong")
            {
                Assert.Contains(res.Errors!, e => e.StartsWith(VisitExecutionErrorCodes.ScannedAccountPrefix, StringComparison.Ordinal));
            }

            return;
        }

        var dto = res.Data!;
        Assert.Equal(qrStatus, dto.Qr!.Status);
        Assert.Equal(qrStatus, _executions.Items.Single().Qr!.Status);
        var qrFlags = dto.Flags.Where(f => f.StartsWith("qr_", StringComparison.Ordinal)).ToList();
        Assert.Equal(codeOrFlag is null ? Array.Empty<string>() : new[] { codeOrFlag }, qrFlags);
        if (scenario is not ("off" or "remote"))
        {
            Assert.Equal(DateTimeOffset.Parse("2026-10-15T08:59:30Z"), dto.Qr.ScannedAt); // the client's instant, informative only
        }
        if (qrStatus == VisitQrScanStatuses.Ok)
        {
            Assert.Equal(VisitQrMatchedVia.Self, dto.Qr.MatchedVia);
            Assert.Equal(account, dto.Qr.AccountId);
        }
    }

    [Fact]
    public async Task Qr_runs_after_the_location_rule_and_before_the_photo_rule()
    {
        QrMode(VisitQrModes.Required, VisitPhotoModes.Required);
        var plan = Seed();
        await IssueAsync(plan.AccountId!.Value);
        AssertCode(await QrStarter().Handle(StartQr(plan, null, lat: FarLat), default), 409, VisitExecutionErrorCodes.LocationTooFar);
        AssertCode(await QrStarter().Handle(StartQr(plan, null), default), 400, VisitExecutionErrorCodes.QrRequired); // not photo_required
        AssertCode(await QrStarter().Handle(StartQr(plan, await PayloadAsync(plan.AccountId!.Value)), default), 400, VisitExecutionErrorCodes.PhotoRequired);
    }

    [Fact]
    public async Task Qr_an_ok_scan_counts_on_the_code_and_a_counter_failure_never_blocks_the_start()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        await IssueAsync(plan.AccountId!.Value);
        var payload = await PayloadAsync(plan.AccountId!.Value);
        Assert.Equal(201, (await QrStarter().Handle(StartQr(plan, payload), default)).StatusCode);
        var code = _qrCodes.Items.Single();
        Assert.Equal((1, Now), (code.ScanCount, code.LastScannedAt!.Value));

        var second = Seed();
        second.AccountId = plan.AccountId; // the same institution, another visit
        _qrCodes.ThrowOnScan = true;
        Assert.Equal(201, (await QrStarter().Handle(StartQr(second, payload), default)).StatusCode);
    }

    // ═══ the parent chain (3) ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Qr_a_sub_unit_visit_accepts_the_parent_institutions_code()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        var hospital = NewAccount();
        AccountOf(plan).ParentAccountId = hospital.Id;
        await IssueAsync(hospital.Id);

        var res = await QrStarter().Handle(StartQr(plan, await PayloadAsync(hospital.Id)), default);
        Assert.Equal(201, res.StatusCode);
        Assert.Equal((VisitQrScanStatuses.Ok, VisitQrMatchedVia.Parent, hospital.Id), (res.Data!.Qr!.Status, res.Data.Qr.MatchedVia, res.Data.Qr.AccountId));
        var state = (await new GetVisitQrAccountHandler(QrService()).Handle(new GetVisitQrAccountQuery(plan.AccountId!.Value), default)).Data!;
        Assert.Equal((InstitutionQrCodeStatuses.None, hospital.Id), (state.Status, state.InheritedFrom!.AccountId)); // inheritedFrom
    }

    [Fact]
    public async Task Qr_a_cyclic_parent_chain_ends_without_looping()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        var p1 = NewAccount(parent: plan.AccountId);
        AccountOf(plan).ParentAccountId = p1.Id; // child → p1 → child …
        var task = QrStarter().Handle(StartQr(plan, null), default);
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(5000)));
        var res = await task;
        Assert.Equal(201, res.StatusCode);
        Assert.Equal(VisitQrScanStatuses.NotIssued, res.Data!.Qr!.Status);
        var chain = await new VisitQrVerifier(_qrCodes, _accounts, _qrKeys).ChainAsync(Tenant, plan.AccountId, default);
        Assert.Equal(new[] { plan.AccountId!.Value, p1.Id }, chain);
    }

    [Fact]
    public async Task Qr_the_fifth_parent_counts_and_the_sixth_does_not()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        var parents = new List<Account>();
        var child = AccountOf(plan);
        for (var i = 0; i < 6; i++)
        {
            var p = NewAccount();
            child.ParentAccountId = p.Id;
            parents.Add(p);
            child = p;
        }

        await IssueAsync(parents[5].Id); // the 6th level
        var sixth = await PayloadAsync(parents[5].Id);
        var refused = await QrStarter().Handle(StartQr(plan, sixth), default);
        AssertCode(refused, 409, VisitExecutionErrorCodes.QrWrongAccount);

        await IssueAsync(parents[4].Id); // the 5th level
        var ok = await QrStarter().Handle(StartQr(plan, await PayloadAsync(parents[4].Id)), default);
        Assert.Equal((201, VisitQrMatchedVia.Parent), (ok.StatusCode, ok.Data!.Qr!.MatchedVia));
    }

    // ═══ the exception, concurrency, bulk, keys, tenants ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Qr_the_qr_optional_exception_turns_required_into_optional_and_shows_its_source()
    {
        QrMode(VisitQrModes.Required);
        var plan = Seed();
        await IssueAsync(plan.AccountId!.Value);
        AssertCode(await QrStarter().Handle(StartQr(plan, null), default), 400, VisitExecutionErrorCodes.QrRequired);

        var exception = Exception(VisitEvidenceExceptionStatuses.Approved, Today, Today, Rep, VisitEvidenceExceptionRules.QrOptional);
        var res = await QrStarter().Handle(StartQr(plan, null), default);
        Assert.Equal(201, res.StatusCode);
        Assert.Equal(VisitQrScanStatuses.Missing, res.Data!.Qr!.Status);
        Assert.Equal(new[] { VisitEvidenceExceptionRules.QrOptional }, res.Data.RelaxedRules);

        var effective = await new VisitEvidencePolicyResolver(_policies, _exceptionStore).ResolveAsync(Tenant, Rep, Now, default);
        var qr = effective.Fields.Single(f => f.Field == "qrMode");
        Assert.Equal((VisitQrModes.Optional, EvidenceFieldSources.Exception, (Guid?)exception.Id), (qr.Value, qr.Source, qr.ExceptionId));

        // an off tenant stays off under the exception (it only relaxes required)
        _policies.Stored!.QrMode = VisitQrModes.Off;
        var off = await new VisitEvidencePolicyResolver(_policies, _exceptionStore).ResolveAsync(Tenant, Rep, Now, default);
        Assert.Equal((VisitQrModes.Off, EvidenceFieldSources.Tenant), (off.Policy.QrMode, off.Fields.Single(f => f.Field == "qrMode").Source));
    }

    [Fact]
    public async Task Qr_two_concurrent_issues_leave_one_active_code_and_the_loser_gets_409()
    {
        var account = NewAccount();
        var arrived = 0;
        var gate = new TaskCompletionSource();
        _qrCodes.BeforeInsert = async () =>
        {
            if (Interlocked.Increment(ref arrived) == 2) gate.TrySetResult();
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        };
        var results = await Task.WhenAll(IssueAsync(account.Id), IssueAsync(account.Id));
        Assert.Equal(new[] { 201, 409 }, results.Select(r => r.StatusCode).OrderBy(s => s));
        Assert.Contains(VisitExecutionErrorCodes.ConcurrencyConflict, results.Single(r => r.StatusCode == 409).Errors!);
        Assert.Single(_qrCodes.Items, c => c.IsActive());

        // two concurrent regenerates at the same expected version: one wins
        _qrCodes.BeforeInsert = null;
        var regen = await Task.WhenAll(IssueAsync(account.Id, expected: 1), IssueAsync(account.Id, expected: 1));
        Assert.Equal(new[] { 201, 409 }, regen.Select(r => r.StatusCode).OrderBy(s => s));
        Assert.Single(_qrCodes.Items, c => c.IsActive());
        AssertCode(await IssueAsync(account.Id), 409, VisitExecutionErrorCodes.ConcurrencyConflict); // active + no expectedVersion
    }

    [Fact]
    public async Task Qr_bulk_issue_skips_active_codes_names_the_unknown_and_stops_at_200()
    {
        var withCode = NewAccount();
        await IssueAsync(withCode.Id);
        var fresh = NewAccount();
        var foreign = NewAccount(tenant: OtherTenant);
        var unknown = Guid.NewGuid();
        var service = QrService();

        var tooMany = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList();
        AssertCode(await new BulkIssueVisitQrHandler(service).Handle(new BulkIssueVisitQrCommand(new VisitQrBulkInput(tooMany)), default), 400, VisitExecutionErrorCodes.QrRequestInvalid);
        AssertCode(await new BulkIssueVisitQrHandler(service).Handle(new BulkIssueVisitQrCommand(new VisitQrBulkInput(Array.Empty<Guid>())), default), 400, VisitExecutionErrorCodes.QrRequestInvalid);
        var exactly200 = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToList();
        Assert.Equal(200, (await new BulkIssueVisitQrHandler(service).Handle(new BulkIssueVisitQrCommand(new VisitQrBulkInput(exactly200)), default)).Data!.NotFound);

        var res = (await new BulkIssueVisitQrHandler(service).Handle(
            new BulkIssueVisitQrCommand(new VisitQrBulkInput(new[] { withCode.Id, fresh.Id, foreign.Id, unknown, fresh.Id })), default)).Data!;
        Assert.Equal((1, 1, 2), (res.Issued, res.SkippedActive, res.NotFound));
        Assert.Equal(new[] { VisitQrBulkResults.SkippedActive, VisitQrBulkResults.Issued, VisitQrBulkResults.NotFound, VisitQrBulkResults.NotFound },
            res.Items.Select(i => i.Result));
        Assert.Single(_qrEvents.Items, e => e.Kind == InstitutionQrEventKinds.BulkIssued);
        Assert.DoesNotContain(_qrCodes.Items, c => c.AccountId == foreign.Id);

        var payloads = (await new BulkVisitQrPayloadHandler(service).Handle(
            new BulkVisitQrPayloadCommand(new VisitQrBulkInput(new[] { withCode.Id, fresh.Id, foreign.Id, unknown })), default)).Data!;
        Assert.Equal(new[] { withCode.Id, fresh.Id }, payloads.Select(p => p.AccountId));
        Assert.All(payloads, p => Assert.NotNull(VisitQrSigner.Verify(_qrKeys.Keys, Tenant, p.Payload)));
        Assert.Equal(2, _qrEvents.Items.Count(e => e.Kind == InstitutionQrEventKinds.Printed));
    }

    [Fact]
    public void Qr_routes_reads_need_read_and_every_write_or_payload_needs_manage()
    {
        var actions = typeof(VisitQrController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(h => (Verb: h.HttpMethods.Single(), h.Template, Keys: m.GetCustomAttributes<HasPermissionAttribute>().Select(p => p.Permission).ToList())))
            .ToDictionary(a => a.Verb + " " + a.Template, a => a.Keys);
        Assert.Equal(new[] { VisitQrPermissions.Read }, actions["GET api/crm/visit-qr/accounts"]);
        Assert.Equal(new[] { VisitQrPermissions.Read }, actions["GET api/crm/visit-qr/accounts/{accountId:guid}"]);
        Assert.Equal(new[] { VisitQrPermissions.Read }, actions["GET api/crm/visit-qr/accounts/{accountId:guid}/history"]);
        Assert.Equal(new[] { VisitQrPermissions.Manage }, actions["GET api/crm/visit-qr/accounts/{accountId:guid}/payload"]); // .read never sees a payload
        Assert.Equal(new[] { VisitQrPermissions.Manage }, actions["POST api/crm/visit-qr/accounts/{accountId:guid}/issue"]);
        Assert.Equal(new[] { VisitQrPermissions.Manage }, actions["POST api/crm/visit-qr/accounts/{accountId:guid}/revoke"]);
        Assert.Equal(new[] { VisitQrPermissions.Manage }, actions["POST api/crm/visit-qr/bulk-issue"]);
        Assert.Equal(new[] { VisitQrPermissions.Manage }, actions["POST api/crm/visit-qr/bulk-payload"]);
        Assert.Equal(8, actions.Count);
        Assert.DoesNotContain(actions.Keys, k => k.StartsWith("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Qr_another_tenants_institution_is_404_everywhere()
    {
        var foreign = NewAccount(tenant: OtherTenant);
        var service = QrService();
        AssertCode(await new GetVisitQrAccountHandler(service).Handle(new GetVisitQrAccountQuery(foreign.Id), default), 404, VisitExecutionErrorCodes.QrAccountNotFound);
        AssertCode(await new GetVisitQrHistoryHandler(service).Handle(new GetVisitQrHistoryQuery(foreign.Id), default), 404, VisitExecutionErrorCodes.QrAccountNotFound);
        AssertCode(await IssueAsync(foreign.Id), 404, VisitExecutionErrorCodes.QrAccountNotFound);
        AssertCode(await new GetVisitQrPayloadHandler(service).Handle(new GetVisitQrPayloadCommand(foreign.Id), default), 404, VisitExecutionErrorCodes.QrAccountNotFound);
        Assert.Empty(_qrCodes.Items);
    }

    [Fact]
    public async Task Qr_without_a_key_issue_and_print_answer_503_and_nothing_is_written()
    {
        var account = NewAccount();
        AssertCode(await IssueAsync(account.Id, keys: TestQrKeys.None()), 503, VisitExecutionErrorCodes.QrSigningUnavailable);
        AssertCode(await new BulkIssueVisitQrHandler(QrService(TestQrKeys.None())).Handle(new BulkIssueVisitQrCommand(new VisitQrBulkInput(new[] { account.Id })), default),
            503, VisitExecutionErrorCodes.QrSigningUnavailable);
        Assert.Empty(_qrCodes.Items);
        await IssueAsync(account.Id);
        AssertCode(await new GetVisitQrPayloadHandler(QrService(TestQrKeys.None())).Handle(new GetVisitQrPayloadCommand(account.Id), default), 503, VisitExecutionErrorCodes.QrSigningUnavailable);
        Assert.DoesNotContain(_qrEvents.Items, e => e.Kind == InstitutionQrEventKinds.Printed);
    }

    [Fact]
    public async Task Qr_the_list_validates_its_filters_before_reading()
    {
        var handler = new ListVisitQrAccountsHandler(QrService(), null!, null!, null!);
        AssertCode(await handler.Handle(new ListVisitQrAccountsQuery("lost", null, null, null, null, 1, 25), default), 400, VisitExecutionErrorCodes.QrRequestInvalid);
        AssertCode(await handler.Handle(new ListVisitQrAccountsQuery(null, null, null, null, null, 1, 101), default), 400, VisitExecutionErrorCodes.QrRequestInvalid);
        AssertCode(await handler.Handle(new ListVisitQrAccountsQuery(null, null, null, "not-a-guid", null, 1, 25), default), 400, VisitExecutionErrorCodes.QrRequestInvalid);
    }

    // ═══ backward compatibility + storage ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Qr_off_by_default_an_old_policy_document_reads_off_and_the_history_names_qrMode()
    {
        Assert.Equal(VisitQrModes.Off, new VisitEvidencePolicy().QrMode);
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var old = new BsonDocument { { "_id", Guid.NewGuid().ToString("D") }, { "TenantId", Tenant.ToString("D") }, { "LocationMode", "info" }, { "PhotoMode", "optional" } };
        Assert.Equal(VisitQrModes.Off, BsonSerializer.Deserialize<VisitEvidencePolicy>(old).QrMode);

        Assert.Contains(("qrMode", VisitQrModes.Off), VisitEvidencePolicyResolver.Values(new VisitEvidencePolicy()));
        var target = new VisitEvidencePolicy();
        Assert.Null(VisitExecutionRules.ApplyPolicy(target, new VisitEvidencePolicyInput(null, null, null, null, null, null, null, null, null, QrMode: "REQUIRED")));
        Assert.Equal(VisitQrModes.Required, target.QrMode);
        Assert.Equal(VisitQrModes.Required, VisitExecutionRules.ToDto(target, false).QrMode);
        Assert.Equal(VisitExecutionErrorCodes.PolicyInvalid,
            VisitExecutionRules.ApplyPolicy(new VisitEvidencePolicy(), new VisitEvidencePolicyInput(null, null, null, null, null, null, null, null, null, QrMode: "block"))!.Code);

        // a tenant left at off starts exactly as before: no QR verdict beyond not_applicable, no QR flag, the payload ignored
        var plan = Seed();
        var res = await QrStarter().Handle(StartQr(plan, "anything"), default);
        Assert.Equal(201, res.StatusCode);
        Assert.Equal(VisitQrScanStatuses.NotApplicable, res.Data!.Qr!.Status);
        Assert.DoesNotContain(res.Data.Flags, f => f.StartsWith("qr_", StringComparison.Ordinal));

        var contract = (await new GetVisitExecutionContractHandler(TenantCtx(), _policies, RepCaller(), null, _clock).Handle(new GetVisitExecutionContractQuery(), default)).Data!;
        Assert.Equal(VisitQrModes.All, contract.QrModes);
        Assert.Equal(VisitQrScanStatuses.All, contract.QrStatuses);
        Assert.Equal(200, contract.MaxQrPayloadLength);
        Assert.Equal(VisitQrPermissions.All, contract.QrPermissions);
        Assert.Contains(VisitExecutionErrorCodes.QrWrongAccount, contract.ErrorCodes);
        Assert.Contains(VisitExecutionFlags.QrNotIssued, contract.FlagCodes);
        Assert.Equal(VisitQrModes.Off, contract.Policy.QrMode);
    }

    [Fact]
    public void Qr_every_guid_is_a_string_and_the_documents_round_trip()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var code = new InstitutionQrCode { TenantId = Tenant, AccountId = Guid.NewGuid(), CodeVersion = 2, KeyId = "k1", IssuedAt = Now };
        var doc = code.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["AccountId"].BsonType);
        Assert.Equal(code.AccountId, BsonSerializer.Deserialize<InstitutionQrCode>(doc).AccountId);

        var ev = new InstitutionQrEvent { TenantId = Tenant, AccountId = Guid.NewGuid(), QrCodeId = Guid.NewGuid(), Kind = "issued", At = Now };
        var evDoc = ev.ToBsonDocument();
        Assert.Equal((BsonType.String, BsonType.String), (evDoc["AccountId"].BsonType, evDoc["QrCodeId"].BsonType));

        var execution = new Domain.Entities.VisitExecution { TenantId = Tenant, PlannedVisitId = Guid.NewGuid(), Qr = new VisitExecutionQr { Status = "ok", AccountId = Guid.NewGuid(), Version = 1, MatchedVia = "self" } };
        var exDoc = execution.ToBsonDocument();
        Assert.Equal(BsonType.String, exDoc["Qr"]["AccountId"].BsonType);
        Assert.Equal(execution.Qr.AccountId, BsonSerializer.Deserialize<Domain.Entities.VisitExecution>(exDoc).Qr!.AccountId);
    }
}
