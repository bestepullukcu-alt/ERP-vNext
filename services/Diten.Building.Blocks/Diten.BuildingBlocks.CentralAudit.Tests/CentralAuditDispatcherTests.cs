using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.BuildingBlocks.CentralAudit.Tests;

/// <summary>
/// BL-501 — delivery. Platform 2xx marks the row; Platform down / 5xx leaves it and retries later; 400 leaves it as
/// Rejected; no address / key: nothing is sent, nothing is lost, and it is said out loud (log + health).
/// </summary>
public sealed class CentralAuditDispatcherTests
{
    [Fact]
    public async Task Platform_accepts_the_row_is_marked_delivered_and_the_request_has_the_append_shape()
    {
        using var harness = new Harness();
        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));
        harness.Platform.Answer(HttpStatusCode.Accepted, "{\"status\":\"Queued\"}");

        Assert.Equal(1, await harness.DispatchAsync());

        var row = Assert.Single(harness.Store.Rows);
        Assert.Equal(CentralAuditOutboxStatus.Delivered, row.Status);
        var (request, body) = Assert.Single(harness.Platform.Calls);
        Assert.Equal("http://platform.test/api/internal/audit/append", request.RequestUri!.ToString());
        Assert.Equal(Harness.Key, Assert.Single(request.Headers.GetValues("X-Internal-Api-Key")));

        // JsonContent writes web (camelCase) names; Platform's model binder reads them case-insensitively.
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(row.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal(Harness.Tenant, root.GetProperty("targetTenantId").GetGuid());
        Assert.False(root.GetProperty("isPlatformGlobal").GetBoolean());
        Assert.Equal(14, root.GetProperty("category").GetInt32()); // MasterData
        Assert.Equal(1, root.GetProperty("operation").GetInt32()); // Create
        Assert.Equal(3, root.GetProperty("actorType").GetInt32()); // TenantUser
        Assert.Equal(1, root.GetProperty("outcome").GetInt32());   // Succeeded
        Assert.Equal(Harness.User, root.GetProperty("actorId").GetGuid());
        Assert.Equal(CreateWidgetCommand.CreatedId, root.GetProperty("entityId").GetGuid());
        Assert.Equal("Widget", root.GetProperty("entityType").GetString());
        Assert.Equal("Diten.TestService", root.GetProperty("sourceService").GetString());
        Assert.Equal("widget-catalog", root.GetProperty("sourceModule").GetString());
        Assert.Equal(Harness.Correlation, root.GetProperty("correlationId").GetGuid());
        Assert.Equal(Harness.Company, root.GetProperty("metadata").GetProperty("companyId").GetGuid());
    }

    /// <summary>s3 — a failed delivery never removes the row: it is sent later.</summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData((HttpStatusCode)0)] // unreachable
    public async Task Platform_down_or_failing_the_row_stays_with_a_backed_off_retry_and_is_delivered_later(HttpStatusCode failure)
    {
        using var harness = new Harness();
        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));
        if (failure == 0)
        {
            harness.Platform.Throw();
        }
        else
        {
            harness.Platform.Answer(failure);
        }

        Assert.Equal(0, await harness.DispatchAsync());

        var waiting = Assert.Single(harness.Store.Rows);
        Assert.Equal(CentralAuditOutboxStatus.Pending, waiting.Status);
        Assert.Equal(1, waiting.Attempts);
        Assert.True(waiting.NextAttemptAtUtc > harness.Clock.GetUtcNow().UtcDateTime);
        Assert.False(string.IsNullOrWhiteSpace(waiting.LastError));
        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Warning
                                                   && e.Message.Contains(waiting.Id.ToString(), StringComparison.Ordinal)
                                                   && e.Message.Contains(Harness.Tenant.ToString(), StringComparison.Ordinal));

        // Not before its time…
        Assert.Equal(0, await harness.DispatchAsync());
        Assert.Single(harness.Platform.Calls);

        // …and then delivered.
        harness.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await harness.DispatchAsync());
        Assert.Equal(CentralAuditOutboxStatus.Delivered, Assert.Single(harness.Store.Rows).Status);
    }

    [Fact]
    public async Task Platform_refuses_the_row_stays_as_rejected_and_is_logged_as_an_error()
    {
        using var harness = new Harness();
        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));
        harness.Platform.Answer(HttpStatusCode.BadRequest, "{\"status\":\"Rejected\"}");

        await harness.DispatchAsync();

        var row = Assert.Single(harness.Store.Rows);
        Assert.Equal(CentralAuditOutboxStatus.Rejected, row.Status);
        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains(row.Id.ToString(), StringComparison.Ordinal));
        var health = await harness.Health().CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, health.Status);
    }

    [Fact]
    public async Task Without_address_or_key_commands_run_events_accumulate_nothing_is_sent_and_it_is_said_out_loud()
    {
        using var harness = new Harness(configured: false);

        var result = await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));
        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));
        await harness.DispatchAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, harness.Store.Rows.Count(r => r.Status == CentralAuditOutboxStatus.Pending));
        Assert.Empty(harness.Platform.Calls);
        Assert.Contains(harness.Logs.Entries, e => e.Level == LogLevel.Warning
                                                   && e.Message.Contains("NOT DELIVERING", StringComparison.Ordinal)
                                                   && e.Message.Contains("2 event(s)", StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logs.Entries, e => e.Message.Contains(Harness.Key, StringComparison.Ordinal));

        var health = await harness.Health().CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, health.Status);
        Assert.Equal(false, health.Data["configured"]);
        Assert.Equal(2L, health.Data["pending"]);
    }

    [Fact]
    public async Task An_undeclared_category_never_reaches_platform_and_stays_as_rejected()
    {
        using var harness = new Harness();
        await harness.SendAsync(new CreateWidgetCommand(Ending.Succeeds));
        harness.Store.Rows[0].Category = "Inventory"; // not a Platform AuditCategory name

        await harness.DispatchAsync();

        Assert.Empty(harness.Platform.Calls);
        Assert.Equal(CentralAuditOutboxStatus.Rejected, harness.Store.Rows[0].Status);
    }

    [Fact]
    public void Backoff_grows_and_is_capped()
    {
        var options = new CentralAuditOptions { BaseBackoff = TimeSpan.FromSeconds(5), MaxBackoff = TimeSpan.FromMinutes(10) };

        Assert.Equal(TimeSpan.FromSeconds(5), options.BackoffFor(1));
        Assert.Equal(TimeSpan.FromSeconds(10), options.BackoffFor(2));
        Assert.Equal(TimeSpan.FromSeconds(40), options.BackoffFor(4));
        Assert.Equal(TimeSpan.FromMinutes(10), options.BackoffFor(30));
    }

    /// <summary>The mirror of Platform's enums is read against Platform's own source file.</summary>
    [Fact]
    public void The_platform_enum_mirror_matches_AuditEnums_cs()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Domain", "Enums", "AuditEnums.cs"));

        Assert.Equal(Members(source, "AuditCategory"), CentralAuditPlatformValues.Categories.OrderBy(p => p.Key).ToList());
        Assert.Equal(Members(source, "AuditOperation"), CentralAuditPlatformValues.Operations.OrderBy(p => p.Key).ToList());
        Assert.Equal(Members(source, "AuditActorType"), CentralAuditPlatformValues.ActorTypes.OrderBy(p => p.Key).ToList());
        var outcomes = Members(source, "AuditOutcome").ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(CentralAuditPlatformValues.OutcomeSucceeded, outcomes["Succeeded"]);
        Assert.Equal(CentralAuditPlatformValues.OutcomeFailed, outcomes["Failed"]);
    }

    private static List<KeyValuePair<string, int>> Members(string source, string enumName)
    {
        var body = Regex.Match(source, @"enum\s+" + enumName + @"\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        Assert.False(string.IsNullOrWhiteSpace(body), $"enum {enumName} not found");
        return Regex.Matches(Regex.Replace(body, @"///[^\n]*|//[^\n]*", string.Empty), @"(\w+)\s*=\s*(\d+)")
            .Select(m => new KeyValuePair<string, int>(m.Groups[1].Value, int.Parse(m.Groups[2].Value)))
            .Where(p => p.Key != "Unknown")
            .OrderBy(p => p.Key)
            .ToList();
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root (AGENTS.md) not found");
    }
}
