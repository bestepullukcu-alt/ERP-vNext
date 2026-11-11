using System.Security.Claims;
using System.Text.RegularExpressions;
using Diten.Web.Models;
using Diten.Web.Models.Inventory;
using Diten.Web.Tests.Traceability;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-MVP1-NAMES1 — FU-0173-UI-USER-01 (owner decision A): "Kaydeden" (MOD-0173-FU01 §7 BG-05 "Recorded by") on the movement history and on the
/// receipt. The history reads INVENTORY 1.5.0 <c>createdBy</c> (the name at the time of the posting); the receipt is the movement the signed-in
/// user just posted, named by their token the way the service names the posting. No name → the 7-language "unknown user"; the id is only the
/// hover title, never a name.
/// </summary>
public sealed class InventoryRecordedByTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Id = "0d9c8b7a-6f5e-4d3c-8b1a-0f9e8d7c6b5a";
    private readonly WebApplicationFactory<Program> _factory;

    public InventoryRecordedByTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void The_history_row_carries_the_recorded_poster_and_name()
    {
        var rows = InventoryListAdapter.MapTransactions(
        [
            new TransactionItemDto { TransactionId = "a", MovementType = "COUNT_LOSS", SourceModule = "MOD-0173", CreatedBy = new() { UserId = Id, Name = " Ayşe Demir " } },
            new TransactionItemDto { TransactionId = "b", MovementType = "COUNT_LOSS", SourceModule = "MOD-0173", CreatedBy = new() { UserId = Id } },
            new TransactionItemDto { TransactionId = "c", MovementType = "COUNT_LOSS", SourceModule = "MOD-0173" }
        ], MasterLabels.None).ToDictionary(r => r.Id);
        Assert.Equal((Id, "Ayşe Demir"), (rows["a"].CreatedById, rows["a"].CreatedByName));
        Assert.Equal((Id, (string?)null), (rows["b"].CreatedById, rows["b"].CreatedByName));
        Assert.Equal(((string?)null, (string?)null), (rows["c"].CreatedById, rows["c"].CreatedByName));
    }

    [Theory]
    [InlineData("name", "Ayşe Demir", null, null, "Ayşe Demir")]
    [InlineData(null, null, "Ayşe", "Demir", "Ayşe Demir")]
    [InlineData(null, null, null, null, null)]
    public void The_receipt_names_the_poster_with_the_token_name_rule_of_the_service(string? nameType, string? name, string? given, string? family, string? expected)
    {
        var claims = new List<Claim> { new("sub", Id) };
        if (nameType is not null) claims.Add(new Claim(nameType, name!));
        if (given is not null) claims.Add(new Claim(ClaimTypes.GivenName, given));
        if (family is not null) claims.Add(new Claim("family_name", family));
        Assert.Equal(expected, MovementReceiptViewModel.PostingName(new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))));
    }

    [Fact]
    public async Task The_receipt_shows_the_name_else_unknown_user_with_the_id_only_as_title()
    {
        const string view = "/Views/Inventory/StockMovements/Details.cshtml";
        var named = System.Net.WebUtility.HtmlDecode(await TraceViewRenderer.RenderAsync(_factory, view, new MovementReceiptViewModel { TransactionNumber = "INV-1", RecordedById = Id, RecordedByName = "Ayşe Demir" }, "inventory.movements.create"));
        Assert.Matches(new Regex(@"data-recorded-by[^>]*>Ayşe Demir</div>"), named);
        var bare = await TraceViewRenderer.RenderAsync(_factory, view, new MovementReceiptViewModel { TransactionNumber = "INV-1", RecordedById = Id }, "inventory.movements.create");
        Assert.Matches(new Regex($@"data-recorded-by title=""{Id}"">UnknownUser</div>|data-recorded-by title=""{Id}"">Unknown user</div>"), bare);
        Assert.DoesNotMatch(new Regex($@"data-recorded-by[^>]*>{Id}<"), bare);
    }

    [Fact]
    public async Task The_history_has_the_recorded_by_column_and_loads_its_rule_before_the_list()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Inventory/StockMovements/Index.cshtml", null, "inventory.stock.read");
        var people = html.IndexOf("/assets/js/Inventory/StockMovements/people.js", StringComparison.Ordinal);
        var index = html.IndexOf("/assets/js/Inventory/StockMovements/index.js", StringComparison.Ordinal);
        Assert.True(people > 0 && index > people, "people.js must load before index.js");
        Assert.Matches(new Regex(@"<th[^>]*>\s*(ColRecordedBy|Recorded by)\s*</th>"), html);
        Assert.Contains("\"unknownUser\":", html, StringComparison.Ordinal); // camelCase on the wire; index.l10n.js reads it as UnknownUser
    }

    [Fact]
    public async Task The_recorded_by_rule_holds_against_the_shipped_script()
    {
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = TraceViewRenderer.RepoRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add("--test-reporter=tap");
        start.ArgumentList.Add(Path.Combine("frontend", "Diten.Web.Tests", "JavaScript", "Node", "inventory-names1.test.mjs"));
        using var node = System.Diagnostics.Process.Start(start)!;
        var error = node.StandardError.ReadToEndAsync();
        var output = await node.StandardOutput.ReadToEndAsync();
        await node.WaitForExitAsync();
        Assert.True(node.ExitCode == 0, $"{output}\n{await error}");
        Assert.Matches(new Regex(@"^# pass 2$", RegexOptions.Multiline), output);
        Assert.Matches(new Regex(@"^# fail 0$", RegexOptions.Multiline), output);
    }
}
