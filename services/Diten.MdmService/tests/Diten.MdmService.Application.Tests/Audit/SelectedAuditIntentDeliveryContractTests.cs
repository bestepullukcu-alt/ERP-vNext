using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.Audit;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class SelectedAuditIntentDeliveryContractTests
{
    private const string Marker = "test-owned-one-shot-marker";
    private const string Sid = "S-1-5-21-100-200-300-1001";
    private const string Account = "TESTDOMAIN\\operator";

    [Theory]
    [InlineData(ProductAuditOperation.GlobalProductDraftCreated, 5)]
    [InlineData(ProductAuditOperation.GlobalProductDraftUpdated, 38)]
    [InlineData(ProductAuditOperation.GlobalProductIdentitySubmitted, 16)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityApproved, 17)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityRejected, 18)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn, 39)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityRetired, 19)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionRequested, 40)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionApplied, 41)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionRejected, 42)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired, 44)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementRequested, 45)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementRejected, 46)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired, 48)]
    public void Global_product_producer_ordinals_remain_exact(ProductAuditOperation operation, int ordinal)
        => Assert.Equal(ordinal, (int)operation);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Selection_count_boundary_is_fail_closed(int count)
    {
        var options = SelectedAuditIntentDeliveryOptions.Capture(EnvironmentValues());
        var prototype = options.Selection.Items[0];
        var items = Enumerable.Range(0, count).Select(_ => prototype with { Locator = prototype.Locator with { IntentId = Guid.NewGuid() } });
        Assert.Throws<ArgumentException>(() => new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), options.Selection.TenantId, items));
    }

    [Fact]
    public void Exactly_one_hundred_unique_locators_are_accepted_and_duplicate_JSON_is_denied()
    {
        var environment = EnvironmentValues();
        var options = SelectedAuditIntentDeliveryOptions.Capture(environment);
        var first = options.Selection.Items[0];
        var items = Enumerable.Range(0, 100).Select(_ => first with { Locator = first.Locator with { IntentId = Guid.NewGuid() } });
        Assert.Equal(100, new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), options.Selection.TenantId, items).Items.Count);
        environment[SelectedAuditIntentDeliveryOptions.ManifestVariable] = environment[SelectedAuditIntentDeliveryOptions.ManifestVariable]!
            .Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => SelectedAuditIntentDeliveryOptions.Capture(environment));
    }

    [Fact]
    public async Task Non_EOF_marker_reader_times_out_before_factory_and_is_eventually_released()
    {
        var environment = EnvironmentValues();
        var manifest = JsonNode.Parse(environment[SelectedAuditIntentDeliveryOptions.ManifestVariable]!)!.AsObject();
        manifest["markerSeconds"] = 1;
        environment[SelectedAuditIntentDeliveryOptions.ManifestVariable] = manifest.ToJsonString();
        var options = SelectedAuditIntentDeliveryOptions.Capture(environment);
        var factories = 0;
        var runner = new SelectedAuditIntentDeliveryRunner(options, () => (Sid, Account),
            _ => { factories++; throw new InvalidOperationException(); });
        using var reader = new NonEofReader();
        using var output = new StringWriter();
        var timer = Stopwatch.StartNew();
        try
        {
            Assert.Equal(2, await runner.RunAsync(reader, output, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.InRange(timer.Elapsed.TotalSeconds, 0.5, 5);
            Assert.Equal(0, factories);
        }
        finally { reader.Release(); }
    }

    private sealed class NonEofReader : TextReader
    {
        private readonly TaskCompletionSource<int> _eof = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task<int> ReadAsync(char[] buffer, int index, int count) => _eof.Task;
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) => new(_eof.Task);
        public void Release() => _eof.TrySetResult(0);
        protected override void Dispose(bool disposing) { Release(); base.Dispose(disposing); }
    }

    [Fact]
    public void Mixed_tenant_negative_generation_and_excluded_types_are_not_permits()
    {
        var options = SelectedAuditIntentDeliveryOptions.Capture(EnvironmentValues());
        var item = options.Selection.Items[0];
        foreach (var invalid in new[] { item with { ExpectedClaimGeneration = -1 }, item with { ExpectedClaimGeneration = long.MaxValue },
            item with { Locator = item.Locator with { TenantId = Guid.NewGuid() } },
            item with { Locator = item.Locator with { AggregateType = AuditAggregateType.FinishedGood } },
            item with { Locator = item.Locator with { AggregateType = AuditAggregateType.ProductLegalEntityScopeRolloutState } } })
            Assert.Throws<ArgumentException>(() => new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), options.Selection.TenantId, [invalid]));
    }

    [Fact]
    public async Task Actual_release_child_rejects_operator_before_normal_host_or_network_composition()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Diten.MdmService.Api.dll");
        Assert.True(File.Exists(dll));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add(SelectedAuditIntentDeliveryCommandLine.ExactArgument);
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("SelectedAuditIntentDelivery", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("AuthTrustedSourceAuditServiceIdentityProvider", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("TrustedSourceAuditIntentClient", StringComparison.OrdinalIgnoreCase)).ToArray()) start.Environment.Remove(key);
        foreach (var pair in EnvironmentValues()) start.Environment[pair.Key] = pair.Value;
        using var child = Process.Start(start)!;
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        await child.StandardInput.WriteAsync(Marker);
        child.StandardInput.Close();
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
        Assert.Equal(2, child.ExitCode);
        var text = await stdout + await stderr;
        Assert.Contains("SELECTED_AUDIT_INTENT_FAILED_CLOSED", text);
        Assert.DoesNotContain("Now listening", text);
        Assert.DoesNotContain("test-only-secret", text);
        Assert.DoesNotContain(Marker, text);
    }

    [Theory]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production")]
    [InlineData("DOTNET_ENVIRONMENT", "development")]
    [InlineData("SelectedAuditIntentDelivery__Enabled", "True")]
    [InlineData("SelectedAuditIntentDelivery__Enabled", "false")]
    [InlineData("SelectedAuditIntentDelivery__Unknown", "true")]
    [InlineData("selectedAuditIntentDelivery__Enabled", "true")]
    public void Options_reject_environment_disable_unknown_and_case_variant(string key, string value)
    {
        var environment = EnvironmentValues();
        environment[key] = value;
        Assert.Throws<InvalidOperationException>(() => SelectedAuditIntentDeliveryOptions.Capture(environment));
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("operatorSid", "\"invalid\"")]
    [InlineData("operatorAccount", "\"operator\"")]
    [InlineData("markerSha256", "\"bad\"")]
    [InlineData("databaseName", "\"admin\"")]
    [InlineData("authBaseUrl", "\"http://example.com/\"")]
    [InlineData("platformBaseUrl", "\"http://127.0.0.1:45001/\"")]
    [InlineData("mongoConnectionString", "\"mongodb://127.0.0.1:45003/\"")]
    [InlineData("leaseSeconds", "9")]
    [InlineData("maximumAttempts", "0")]
    [InlineData("markerSeconds", "31")]
    [InlineData("unknown", "true")]
    public void Options_reject_manifest_drift_before_any_composition(string key, string json)
    {
        var environment = EnvironmentValues();
        var manifest = JsonNode.Parse(environment[SelectedAuditIntentDeliveryOptions.ManifestVariable]!)!.AsObject();
        manifest[key] = JsonNode.Parse(json);
        environment[SelectedAuditIntentDeliveryOptions.ManifestVariable] = manifest.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => SelectedAuditIntentDeliveryOptions.Capture(environment));
    }

    [Theory]
    [InlineData("FinishedGood")]
    [InlineData("ProductLegalEntityScopeRolloutState")]
    [InlineData("globalproduct")]
    [InlineData("1")]
    public void Options_reject_excluded_or_aliased_aggregate(string aggregate)
    {
        var environment = EnvironmentValues();
        environment[SelectedAuditIntentDeliveryOptions.ManifestVariable] = environment[SelectedAuditIntentDeliveryOptions.ManifestVariable]!
            .Replace("GlobalProduct", aggregate, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => SelectedAuditIntentDeliveryOptions.Capture(environment));
    }

    [Fact]
    public void Options_and_request_copy_mutable_inputs_and_reject_duplicate_selection()
    {
        var environment = EnvironmentValues();
        var options = SelectedAuditIntentDeliveryOptions.Capture(environment);
        environment.Clear();
        Assert.Equal(Account, options.OperatorAccount);
        Assert.Single(options.Selection.Items);
        var items = options.Selection.Items.ToList();
        var request = new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), options.Selection.TenantId, items);
        items.Clear();
        Assert.Single(request.Items);
        Assert.Throws<ArgumentException>(() => new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), request.TenantId,
            [request.Items[0], request.Items[0]]));
    }

    [Theory]
    [InlineData("sid")]
    [InlineData("account")]
    [InlineData("account-case")]
    [InlineData("marker")]
    [InlineData("second-line")]
    [InlineData("oversize")]
    [InlineData("empty")]
    [InlineData("cancelled")]
    [InlineData("operator-changed")]
    public async Task Rejected_operator_or_marker_never_constructs_processor(string fault)
    {
        var options = SelectedAuditIntentDeliveryOptions.Capture(EnvironmentValues());
        var constructions = 0;
        var observations = 0;
        var runner = new SelectedAuditIntentDeliveryRunner(options, () =>
        {
            observations++;
            return (fault == "sid" || fault == "operator-changed" && observations > 1 ? Sid + "0" : Sid,
                fault == "account" ? "TESTDOMAIN\\other" : fault == "account-case" ? Account.ToUpperInvariant() : Account);
        }, _ => { constructions++; throw new InvalidOperationException("factory must not run"); });
        var marker = fault switch { "marker" => "wrong", "second-line" => Marker + "\nsecond", "oversize" => new string('a', 1025), "empty" => "", _ => Marker };
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        if (fault == "cancelled") cancellation.Cancel();
        Assert.Equal(2, await runner.RunAsync(new StringReader(marker), output, cancellation.Token));
        Assert.Equal(0, constructions);
        Assert.DoesNotContain(Marker, output.ToString());
        Assert.DoesNotContain("test-only-secret", output.ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(new StringReader(Marker), output, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task Exact_operator_and_one_shot_marker_reach_factory_once_without_secret_output(string suffix)
    {
        var options = SelectedAuditIntentDeliveryOptions.Capture(EnvironmentValues());
        var calls = 0;
        var runner = new SelectedAuditIntentDeliveryRunner(options, () => (Sid, Account),
            _ => { calls++; throw new InvalidOperationException("test-only-secret"); });
        using var output = new StringWriter();
        Assert.Equal(2, await runner.RunAsync(new StringReader(Marker + suffix), output, CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.Equal("SELECTED_AUDIT_INTENT_FAILED_CLOSED" + Environment.NewLine, output.ToString());
    }

    internal static Dictionary<string, string?> EnvironmentValues()
    {
        var tenant = Guid.NewGuid();
        return new(StringComparer.Ordinal)
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            [SelectedAuditIntentDeliveryOptions.EnabledVariable] = "true",
            [SelectedAuditIntentDeliveryOptions.ManifestVariable] = JsonSerializer.Serialize(new
            {
                schemaVersion = 1, executionId = Guid.NewGuid(), tenantId = tenant, operatorSid = Sid,
                operatorAccount = Account, markerSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Marker))),
                mongoConnectionString = "mongodb://127.0.0.1:45003/?replicaSet=testowned&directConnection=true",
                databaseName = "diten_mdm_product_scope_itest", authBaseUrl = "http://127.0.0.1:45001/",
                platformBaseUrl = "http://127.0.0.1:45002/",
                items = new[] { new { tenantId = tenant, aggregateType = "GlobalProduct", aggregateId = Guid.NewGuid(),
                    intentId = Guid.NewGuid(), expectedClaimGeneration = 0L, evidenceFingerprint = new string('A', 64) } }
            }),
            ["AuthTrustedSourceAuditServiceIdentityProvider__AuthBaseUrl"] = "http://127.0.0.1:45001/",
            ["AuthTrustedSourceAuditServiceIdentityProvider__ExpectedIssuer"] = "test-owned-issuer",
            ["AuthTrustedSourceAuditServiceIdentityProvider__ClientId"] = "test-owned-client",
            ["AuthTrustedSourceAuditServiceIdentityProvider__ActiveClientSecret"] = "test-only-secret",
            ["TrustedSourceAuditIntentClient__PlatformBaseUrl"] = "http://127.0.0.1:45002/"
        };
    }
}
