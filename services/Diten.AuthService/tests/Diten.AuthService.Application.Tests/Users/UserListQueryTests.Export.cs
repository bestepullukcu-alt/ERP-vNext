using System.Net;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Diten.AuthService.Application.Features.Users.Models;
using Diten.AuthService.Application.Features.Users.Queries;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-452 package 1 — THE FILE IS THE SCREEN, measured over HTTP (JWT, tenant resolution, [HasPermission]) on the real
/// throwaway mongod. The expectation of every test is either a fact written in <see cref="UserExportWorld"/> or what the
/// LIST endpoint answers for the same parameters — the export is held to the list, never to a copy of its query.
/// </summary>
public sealed partial class UserListQueryTests
{
    private Task<UserExportWorld> ExportWorldAsync() => UserExportWorld.ForAsync(_host);

    private sealed record ExportReply(HttpStatusCode Status, string? ContentType, string? FileName, byte[] Body)
    {
        public string Text => Encoding.UTF8.GetString(Body);
        public List<string[]> Csv => ParseCsv(Text);
        public JsonDocument Json => JsonDocument.Parse(Body);
        public string FirstErrorCode => Json.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString()!;
    }

    private async Task<ExportReply> ExportAsync(string query, string? token = null, string? language = null)
    {
        var world = await ExportWorldAsync();
        using var client = _host.Client(token ?? world.Token);
        if (language is not null) client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.GetAsync("api/users/export" + (query.Length == 0 ? "" : "?" + query));
        return new ExportReply(response.StatusCode, response.Content.Headers.ContentType?.ToString(),
            response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'),
            await response.Content.ReadAsByteArrayAsync());
    }

    private async Task<List<JsonElement>> ListItemsAsync(string query, string? token = null)
    {
        var world = await ExportWorldAsync();
        using var client = _host.Client(token ?? world.Token);
        var body = JsonDocument.Parse(await (await client.GetAsync("api/users?" + query)).Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>RFC 4180 reader — the test's own, so the writer is not measured with itself.</summary>
    private static List<string[]> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add([.. row]); row.Clear(); i++; }
            else field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add([.. row]); }
        return rows;
    }

    // ── the file = the filtered list, not the page ──────────────────────────────────────────────────────

    [Fact]
    public async Task Export_holds_every_row_the_filtered_list_matches_in_the_lists_own_order_not_the_page_on_screen()
    {
        const string filter = "status=Active&orderBy=lastName&orderDir=desc";
        var list = await ListItemsAsync(filter + "&start=0&length=500");
        Assert.Equal(UserExportWorld.ActiveCount, list.Count);

        // The screen shows five per page; the browser sends its page too, as a leaking client would. The file ignores it.
        var reply = await ExportAsync(filter + "&start=0&length=5&columns=email,lastName,status");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        var rows = reply.Csv;
        Assert.Equal(new[] { "Email", "Last Name", "Status" }, rows[0]);
        var data = rows.Skip(1).ToList();
        Assert.Equal(37, data.Count);
        Assert.Equal(list.Select(i => i.GetProperty("email").GetString()), data.Select(r => r[0]));  // same order, same rows
        Assert.All(data, r => Assert.Equal("Active", r[2]));                                         // same filter
    }

    [Fact]
    public async Task Search_and_role_filters_travel_to_the_file_exactly_as_they_do_to_the_list()
    {
        var world = await ExportWorldAsync();
        var auditors = await _host.Database.GetCollection<Role>("roles").Find(r => r.TenantId == world.TenantId && r.Name == "Auditors").SingleAsync();
        var filter = $"search=alphab&roleId={auditors.Id}&orderBy=email&orderDir=asc";

        var list = await ListItemsAsync(filter + "&start=0&length=500");
        var file = (await ExportAsync(filter + "&columns=email,roles")).Csv.Skip(1).ToList();

        Assert.NotEmpty(list);
        Assert.Equal(list.Select(i => i.GetProperty("email").GetString()), file.Select(r => r[0]));
        Assert.All(file, r => Assert.Equal("Auditors", r[1]));
    }

    [Fact]
    public async Task Only_the_requested_columns_reach_the_file_in_the_requested_order()
    {
        var rows = (await ExportAsync("status=Active&columns=status&columns=email")).Csv;

        Assert.Equal(new[] { "Status", "Email" }, rows[0]);
        Assert.All(rows, r => Assert.Equal(2, r.Length));
        // A hidden column's data is nowhere in the file: every last name in this tenant carries "Alpha".
        Assert.DoesNotContain(rows.Skip(1), r => r.Any(cell => cell.Contains("Alpha")));
    }

    [Fact]
    public async Task No_columns_parameter_exports_every_exportable_column_in_the_screen_order()
    {
        var rows = (await ExportAsync("status=Active")).Csv;
        Assert.Equal(new[] { "Email", "First Name", "Last Name", "Roles", "Account Type", "Status" }, rows[0]);
    }

    // ── language, file name, format ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Headers_and_display_values_follow_the_request_culture()
    {
        var tr = (await ExportAsync("status=Invited&columns=email,accountKind,status", language: "tr-TR,tr;q=0.9,en;q=0.5")).Csv;
        Assert.Equal(new[] { "E-posta", "Hesap Türü", "Durum" }, tr[0]);
        Assert.All(tr.Skip(1), r => Assert.Equal(new[] { "Kişi", "Davet edildi" }, r[1..]));
        Assert.Equal(UserExportWorld.InvitedCount, tr.Count - 1);

        var fallback = (await ExportAsync("status=Inactive&columns=status", language: "de-DE")).Csv;
        Assert.Equal(new[] { "Status" }, fallback[0]);                         // not a tenant language → English
        Assert.All(fallback.Skip(1), r => Assert.Equal("Passive", r[0])); // the screen's word for Inactive
    }

    [Fact]
    public async Task Csv_is_utf8_with_a_byte_order_mark_and_named_after_the_screen_and_the_minute()
    {
        var reply = await ExportAsync("status=Active&columns=email", language: "tr");

        Assert.Equal("text/csv; charset=utf-8", reply.ContentType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, reply.Body.Take(3));
        Assert.Matches(@"^users-\d{8}-\d{4}\.csv$", reply.FileName);
    }

    [Fact]
    public async Task Xlsx_holds_the_same_rows_as_text_cells_with_the_header_on_top()
    {
        var reply = await ExportAsync("status=Active&orderBy=email&orderDir=asc&columns=email,roles&format=xlsx");

        Assert.Equal(HttpStatusCode.OK, reply.Status);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", reply.ContentType);
        Assert.Matches(@"^users-\d{8}-\d{4}\.xlsx$", reply.FileName);
        using var workbook = new XLWorkbook(new MemoryStream(reply.Body));
        var sheet = workbook.Worksheets.Single();
        Assert.Equal("Email", sheet.Cell(1, 1).GetString());
        Assert.Equal("Roles", sheet.Cell(1, 2).GetString());
        Assert.Equal(UserExportWorld.ActiveCount + 1, sheet.LastRowUsed()!.RowNumber());
        var list = await ListItemsAsync("status=Active&orderBy=email&orderDir=asc&start=0&length=500");
        Assert.Equal(list.Select(i => i.GetProperty("email").GetString()),
            Enumerable.Range(2, UserExportWorld.ActiveCount).Select(r => sheet.Cell(r, 1).GetString()));
        Assert.All(Enumerable.Range(2, UserExportWorld.ActiveCount), r => Assert.Equal(XLDataType.Text, sheet.Cell(r, 1).DataType));
    }

    // ── tenant, permission, refusals ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_token_exports_none_of_this_tenants_rows()
    {
        var world = await ExportWorldAsync();

        var foreign = (await ExportAsync("search=Alpha&columns=email,lastName", token: world.ForeignToken)).Csv;
        Assert.Single(foreign);                                           // the header, and not one row

        var own = (await ExportAsync("columns=lastName", token: world.ForeignToken)).Csv.Skip(1).ToList();
        Assert.Equal(11, own.Count);                                      // its own eleven, all "Foreign"
        Assert.All(own, r => Assert.Contains("Foreign", r[0]));
    }

    [Fact]
    public async Task Without_the_read_permission_there_is_no_file()
    {
        var world = await ExportWorldAsync();
        var reply = await ExportAsync("status=Active", token: world.NoPermissionToken);
        Assert.Equal(HttpStatusCode.Forbidden, reply.Status);
    }

    [Theory]
    [InlineData("orderBy=passwordHash", "USERS_LIST_ORDER_BY_INVALID")]
    [InlineData("orderBy=email&orderDir=up", "USERS_LIST_ORDER_DIR_INVALID")]
    [InlineData("status=Deleted", "USERS_LIST_STATUS_INVALID")]
    [InlineData("accountKind=Robot", "USERS_LIST_ACCOUNT_KIND_INVALID")]
    public async Task A_bad_list_parameter_is_the_same_400_with_the_same_code_as_on_the_list(string query, string code)
    {
        var world = await ExportWorldAsync();
        using var client = _host.Client(world.Token);
        var listBody = JsonDocument.Parse(await (await client.GetAsync("api/users?start=0&length=10&" + query)).Content.ReadAsStringAsync());

        var reply = await ExportAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(code, reply.FirstErrorCode);
        Assert.Equal(code, listBody.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("format=pdf", "EXPORT_FORMAT_INVALID")]
    [InlineData("columns=email,passwordHash", "EXPORT_COLUMNS_INVALID")]
    [InlineData("columns=tenantId", "EXPORT_COLUMNS_INVALID")]
    public async Task An_unknown_format_or_a_column_outside_the_whitelist_is_refused(string query, string code)
    {
        var reply = await ExportAsync(query);
        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(code, reply.FirstErrorCode);
    }

    [Fact]
    public async Task Over_fifty_thousand_matches_is_413_EXPORT_TOO_LARGE_never_a_truncated_file()
    {
        // A tenant of its own with 50 001 users, written straight into the throwaway database.
        var tenantId = Guid.NewGuid();
        var usersCollection = _host.Database.GetCollection<User>("users");
        var batch = new List<User>(5_000);
        for (var i = 0; i < 50_001; i++)
        {
            batch.Add(new User($"bulk{i:00000}@big.test", "hash:x", "Bulk", $"User{i:00000}", tenantId));
            if (batch.Count == 5_000) { await usersCollection.InsertManyAsync(batch); batch.Clear(); }
        }

        if (batch.Count > 0) await usersCollection.InsertManyAsync(batch);
        var actor = await usersCollection.Find(u => u.TenantId == tenantId && u.Email == "bulk00000@big.test").SingleAsync();

        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(_host.Factory.Services);
        var tokens = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Diten.AuthService.Application.Common.Interfaces.ITokenService>(scope.ServiceProvider);
        var token = tokens.GenerateAccessToken(actor, ["export-reader"], ["auth.users.read"], expiresInMinutes: 60);

        var tooMany = await ExportAsync("columns=email", token: token);
        Assert.Equal((HttpStatusCode)413, tooMany.Status);
        Assert.Equal("EXPORT_TOO_LARGE", tooMany.FirstErrorCode);

        // The same tenant under a filter that brings the match below the ceiling gets its file again.
        var justFits = await ExportAsync("search=User0&columns=email", token: token);
        Assert.Equal(HttpStatusCode.OK, justFits.Status);
        Assert.Equal(10_000, justFits.Csv.Count - 1); // User00000 … User09999
    }

    // ── cost: the roles of the whole file in one query ─────────────────────────────────────────────────

    [Fact]
    public async Task An_export_costs_the_same_number_of_userRoles_queries_for_five_users_as_for_fifty()
    {
        var small = await TenantWithUsersAsync(5);
        var large = await TenantWithUsersAsync(50);
        var (database, log) = CountingDatabase();

        async Task<int> UserRoleQueriesAsync(Guid tenantId, int expectedRows)
        {
            while (log.TryDequeue(out _)) { }
            var result = await HandlerOn(database, tenantId).Handle(new GetAllUsersQuery(List: new UserListRequest(), ExportRowCap: 50_000), CancellationToken.None);
            Assert.True(result.IsSuccessful);
            Assert.Equal(expectedRows, result.Data!.Items.Count);
            Assert.Null(result.Data.Summary); // the file has no header chips
            return log.Count(c => c.Collection == "userRoles");
        }

        Assert.Equal(1, await UserRoleQueriesAsync(small, 5));
        Assert.Equal(1, await UserRoleQueriesAsync(large, 50));
    }

    [Fact]
    public async Task An_export_past_the_cap_reports_the_match_count_and_dresses_no_row()
    {
        var tenantId = await TenantWithUsersAsync(12);
        var (database, log) = CountingDatabase();

        var result = await HandlerOn(database, tenantId).Handle(new GetAllUsersQuery(List: new UserListRequest(), ExportRowCap: 10), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(12, result.Data!.FilteredTotal);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, log.Count(c => c.Collection == "userRoles"));
    }
}
