using System.Net;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Diten.DevEnablementService.Domain.Entities;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.ServerList;

/// <summary>
/// BL-452 package 1 — THE FILE IS THE SCREEN, on the reference list, over HTTP on a real (throwaway) Mongo.
///
/// Data (per run, tenants of this class only): tenant C holds <b>1001</b> live rows EXP-0001…EXP-1001 plus one
/// soft-deleted row; tenant D holds 7 rows named "Foreign …". Every expectation is either a count written here or what the
/// LIST endpoint answers for the same parameters — the export is held to the list, not to a copy of its query.
/// </summary>
[Collection(ServerListCollection.Name)]
public sealed class GoldenCompactExportTests : IAsyncLifetime
{
    private const string List = "/api/golden-reference-compact";
    private const string Export = "/api/golden-reference-compact/export";
    private const string Read = "goldencompact.records.read";
    private const string ExportKey = "goldencompact.records.export";
    private const int Rows = 1001;
    private static readonly string[] Types = ["Standard", "Custom", "Pro"];

    private static readonly Guid TenantC = Guid.NewGuid();
    private static readonly Guid TenantD = Guid.NewGuid();
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static bool _seeded;

    private readonly DevEnablementTestHost _host;

    public GoldenCompactExportTests(DevEnablementTestHost host) => _host = host;

    public async Task InitializeAsync()
    {
        await SeedLock.WaitAsync();
        try
        {
            if (_seeded) return;
            var rows = new List<GoldenReferenceCompact>();
            for (var i = 1; i <= Rows; i++)
            {
                var shuffled = (i * 389) % Rows; // insertion order is not the code order: the file's order must come from orderBy
                rows.Add(new GoldenReferenceCompact
                {
                    TenantId = TenantC,
                    Code = $"EXP-{i:0000}",
                    Name = $"Export row {shuffled:0000}",
                    ReferenceType = Types[i % 3],
                    Category = i % 2 == 0 ? "Alpha" : "Beta",
                    Owner = i % 10 == 0 ? "Ops" : "Finance",   // 100 rows owned by Ops
                    Version = $"=1+{i % 4}",                   // a formula-looking value the file must neutralise
                    Priority = (i % 5) * 10 + 10,
                    IsActive = i % 4 != 0                       // 751 active, 250 passive
                });
            }

            rows.Add(new GoldenReferenceCompact { TenantId = TenantC, Code = "EXP-DEL", Name = "Deleted row", IsDeleted = true, DeletedAt = DateTimeOffset.UtcNow });
            for (var i = 1; i <= 7; i++)
                rows.Add(new GoldenReferenceCompact { TenantId = TenantD, Code = $"FRN-{i:00}", Name = $"Foreign {i}", Owner = "Ops", IsActive = true, Priority = 10 });

            await _host.Collection.InsertManyAsync(rows);
            _seeded = true;
        }
        finally
        {
            SeedLock.Release();
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record Reply(HttpStatusCode Status, string? ContentType, string? FileName, byte[] Body)
    {
        public List<string[]> Csv => ParseCsv(Encoding.UTF8.GetString(Body));
        public string FirstErrorCode => JsonDocument.Parse(Body).RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString()!;
    }

    private async Task<Reply> ExportAsync(string query, Guid? tenant = null, string[]? permissions = null, string? language = null)
    {
        using var client = _host.Client(_host.TokenFor(tenant ?? TenantC, permissions ?? [Read, ExportKey]));
        if (language is not null) client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        var response = await client.GetAsync(Export + (query.Length == 0 ? "" : "?" + query));
        var disposition = response.Content.Headers.ContentDisposition;
        return new Reply(response.StatusCode, response.Content.Headers.ContentType?.ToString(),
            disposition?.FileNameStar ?? disposition?.FileName?.Trim('"'), await response.Content.ReadAsByteArrayAsync());
    }

    /// <summary>Every row the LIST answers for the query, walked page by page (the list pages at most 500).</summary>
    private async Task<List<string>> ListCodesAsync(string query)
    {
        using var client = _host.Client(_host.TokenFor(TenantC, Read));
        var codes = new List<string>();
        for (var start = 0; ; start += 500)
        {
            var body = JsonDocument.Parse(await (await client.GetAsync($"{List}?start={start}&length=500&{query}")).Content.ReadAsStringAsync());
            var data = body.RootElement.GetProperty("data");
            var items = data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()!).ToList();
            codes.AddRange(items);
            if (items.Count < 500 || codes.Count >= data.GetProperty("filteredTotal").GetInt64()) return codes;
        }
    }

    /// <summary>RFC 4180 reader — the test's own, so the writer is not measured with itself.</summary>
    internal static List<string[]> ParseCsv(string text)
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

    [Fact]
    public async Task The_seeded_thousand_and_one_rows_all_reach_the_file_whatever_page_the_screen_shows()
    {
        var whole = await ExportAsync("columns=code");
        Assert.Equal(HttpStatusCode.OK, whole.Status);
        Assert.Equal(Rows, whole.Csv.Count - 1);

        // A client leaking its page (start/length) still gets every row: the parameters are not part of the export.
        var leaked = await ExportAsync("start=0&length=10&columns=code");
        Assert.Equal(Rows, leaked.Csv.Count - 1);
        Assert.DoesNotContain(whole.Csv, r => r[0] == "EXP-DEL");
    }

    [Fact]
    public async Task Filter_search_and_order_give_the_file_the_lists_rows_in_the_lists_order()
    {
        const string query = "status=Active&referenceType=Custom&search=export&orderBy=priority&orderDir=desc";
        var list = await ListCodesAsync(query);
        Assert.InRange(list.Count, 200, 400); // a real subset, not the whole set

        var file = (await ExportAsync(query + "&columns=code,priority,isActive")).Csv;

        Assert.Equal(list, file.Skip(1).Select(r => r[0]));
        Assert.All(file.Skip(1), r => Assert.Equal("Active", r[2]));
    }

    [Fact]
    public async Task Only_the_visible_columns_reach_the_file_in_the_screen_order()
    {
        var file = (await ExportAsync("owner=Ops&columns=isActive,code")).Csv;

        Assert.Equal(new[] { "Status", "Code" }, file[0]);
        Assert.Equal(100, file.Count - 1);
        Assert.All(file, r => Assert.Equal(2, r.Length));
        Assert.DoesNotContain(file.Skip(1), r => r.Any(cell => cell.StartsWith("Export row")));  // Name was hidden
    }

    [Fact]
    public async Task Headers_status_and_reference_type_are_in_the_request_language()
    {
        var file = (await ExportAsync("search=EXP-000&orderBy=code&columns=code,referenceType,isActive", language: "tr-TR")).Csv;

        Assert.Equal(new[] { "Kod", "Referans Tipi", "Durum" }, file[0]);
        // EXP-0001…0009: types by i % 3 (1 → Custom, 2 → Pro, 3 → Standard), passive when i % 4 == 0.
        Assert.Equal(new[] { "EXP-0001", "Özel", "Aktif" }, file[1]);
        Assert.Equal(new[] { "EXP-0003", "Standart", "Aktif" }, file[3]);
        Assert.Equal(new[] { "EXP-0004", "Özel", "Pasif" }, file[4]);
    }

    [Fact]
    public async Task A_formula_looking_cell_is_written_as_text_not_as_a_formula()
    {
        var file = (await ExportAsync("search=EXP-0001&columns=code,version")).Csv;
        Assert.Equal("'=1+1", file[1][1]);
    }

    [Fact]
    public async Task Xlsx_holds_the_same_rows_as_text_cells()
    {
        var reply = await ExportAsync("owner=Ops&orderBy=code&columns=code,priority&format=xlsx");

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", reply.ContentType);
        Assert.Matches(@"^golden-reference-compact-\d{8}-\d{4}\.xlsx$", reply.FileName);
        using var workbook = new XLWorkbook(new MemoryStream(reply.Body));
        var sheet = workbook.Worksheets.Single();
        Assert.Equal(101, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal("EXP-0010", sheet.Cell(2, 1).GetString());
        Assert.Equal(XLDataType.Text, sheet.Cell(2, 2).DataType); // priority 10 stays the text "10"
    }

    [Fact]
    public async Task Csv_carries_a_byte_order_mark_and_the_screen_named_file()
    {
        var reply = await ExportAsync("owner=Ops&columns=code");
        Assert.Equal("text/csv; charset=utf-8", reply.ContentType);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, reply.Body.Take(3));
        Assert.Matches(@"^golden-reference-compact-\d{8}-\d{4}\.csv$", reply.FileName);
    }

    [Fact]
    public async Task Another_tenants_token_exports_none_of_this_tenants_rows()
    {
        var foreign = (await ExportAsync("search=EXP-&columns=code", tenant: TenantD)).Csv;
        Assert.Single(foreign); // header only

        var own = (await ExportAsync("columns=code", tenant: TenantD)).Csv.Skip(1).ToList();
        Assert.Equal(7, own.Count);
        Assert.All(own, r => Assert.StartsWith("FRN-", r[0]));
    }

    [Fact]
    public async Task Reading_the_list_is_not_enough_the_export_key_is_required()
    {
        var reply = await ExportAsync("columns=code", permissions: [Read]);
        Assert.Equal(HttpStatusCode.Forbidden, reply.Status);
    }

    [Theory]
    [InlineData("orderBy=tenantId")]
    [InlineData("orderDir=sideways")]
    [InlineData("status=Deleted")]
    public async Task A_bad_list_parameter_is_the_same_400_as_on_the_list(string query)
    {
        using var client = _host.Client(_host.TokenFor(TenantC, Read));
        var list = await client.GetAsync($"{List}?start=0&length=10&{query}");
        var reply = await ExportAsync(query);

        Assert.Equal(HttpStatusCode.BadRequest, list.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
    }

    [Theory]
    [InlineData("format=docx", "EXPORT_FORMAT_INVALID")]
    [InlineData("columns=code,tenantId", "EXPORT_COLUMNS_INVALID")]
    [InlineData("columns=description", "EXPORT_COLUMNS_INVALID")]
    public async Task An_unknown_format_or_a_column_outside_the_whitelist_is_refused_with_a_code(string query, string code)
    {
        var reply = await ExportAsync(query);
        Assert.Equal(HttpStatusCode.BadRequest, reply.Status);
        Assert.Equal(code, reply.FirstErrorCode);
    }
}
