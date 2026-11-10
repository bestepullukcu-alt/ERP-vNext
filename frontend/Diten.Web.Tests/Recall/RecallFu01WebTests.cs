using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Http;
using Xunit;
using static Diten.Web.Tests.Recall.RecallDataGateTests;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 DL-2 — the bridge that makes `dotnet test` run the FU01 Node tests against the SHIPPED recall scripts (a missing `node`
/// fails here instead of skipping): the customer is a name or a state, never its CRM key; badges; no command on an unresolved or forbidden
/// customer; the customer pages (AC-21…AC-24, AC-27, AC-31 (a)–(d)).
/// </summary>
public sealed class RecallFu01NodeTests
{
    [Fact]
    public async Task The_customer_scripts_hold_against_the_shipped_files()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "recall-fu01.test.mjs"));
        using var node = Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 7$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}

/// <summary>
/// MOD-0177-FU01 AC-31 (e) — the Web door forwards the customer page of the five case parts that carry customers and of the trace read, and
/// the trace file request carries the reader's language (the CSV's customer words; before FU01 the file went without it and came back in en).
/// </summary>
public sealed class RecallCustomerPageWebTests
{
    private const string CaseId = "0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f";
    private const string LotId = "5f4e3d2c-1b0a-4f9e-8d7c-6b5a4f3e2d1c";

    private sealed class Capture : HttpMessageHandler
    {
        public List<(string PathAndQuery, string? Language)> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Sent) Sent.Add((request.RequestUri!.PathAndQuery, request.Headers.TryGetValues("Accept-Language", out var l) ? string.Join(",", l) : null));
            if (request.RequestUri!.AbsolutePath.EndsWith("/trace/export", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("﻿\"x\"\r\n")) { Headers = { ContentType = new("text/csv") } }
                });
            }

            return Task.FromResult(RecallContractFakes.Placeholder(request));
        }
    }

    [Theory]
    [InlineData("customers")]
    [InlineData("scope")]
    [InlineData("diff")]
    [InlineData("returns")]
    [InlineData("effectiveness")]
    public async Task A_case_part_with_customers_forwards_the_customer_page(string part)
    {
        var gateway = new Capture();
        await Invoke(nameof(RecallController.CaseData), gateway, [RecallKeys.CasesRead], $"?part={part}&customerPage=3", id: CaseId, part: part);
        Assert.Contains(gateway.Sent, s => s.PathAndQuery.Contains($"/cases/{CaseId}", StringComparison.Ordinal) && s.PathAndQuery.EndsWith("?customerPage=3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_other_parts_and_a_request_without_a_page_send_none()
    {
        foreach (var part in new[] { "stock", "timeline", "customers" })
        {
            var gateway = new Capture();
            var query = part == "stock" ? "?part=stock&customerPage=3" : part == "timeline" ? "?part=timeline&page=1&pageSize=200&customerPage=3" : "?part=customers";
            await Invoke(nameof(RecallController.CaseData), gateway, [RecallKeys.CasesRead], query, id: CaseId, part: part);
            Assert.DoesNotContain(gateway.Sent, s => s.PathAndQuery.Contains("customerPage", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task The_trace_read_forwards_the_page_and_the_trace_file_the_language()
    {
        var gateway = new Capture();
        await Invoke(nameof(RecallController.TraceData), gateway, [RecallKeys.TraceRun], $"?rootLotId={LotId}&direction=Both&customerPage=2", language: "tr-TR");
        Assert.Contains(gateway.Sent, s => s.PathAndQuery.Contains("/trace?", StringComparison.Ordinal) && s.PathAndQuery.EndsWith("&customerPage=2", StringComparison.Ordinal));

        var files = new Capture();
        await Invoke(nameof(RecallController.ExportTrace), files, [RecallKeys.TraceExport], $"?rootLotId={LotId}&direction=Both", language: "tr-TR");
        var sent = Assert.Single(files.Sent, s => s.PathAndQuery.Contains("/trace/export", StringComparison.Ordinal));
        Assert.Equal("tr-TR", sent.Language);
    }

    private static async Task Invoke(string action, HttpMessageHandler gateway, string[] keys, string query, string? id = null, string? part = null, string? language = null)
    {
        var controller = Build(gateway, keys);
        var http = controller.ControllerContext.HttpContext;
        http.Request.Method = "GET";
        http.Request.QueryString = new QueryString(query);
        if (language is not null) http.Request.Headers.AcceptLanguage = language;
        var q = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query);
        _ = action switch
        {
            nameof(RecallController.CaseData) => await controller.CaseData(id!, part, CancellationToken.None),
            nameof(RecallController.TraceData) => await controller.TraceData(q["rootLotId"], q["direction"], CancellationToken.None),
            _ => await controller.ExportTrace(q["rootLotId"], q["direction"], CancellationToken.None)
        };
    }
}
