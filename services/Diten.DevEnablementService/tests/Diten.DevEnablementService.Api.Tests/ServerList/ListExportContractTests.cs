using System.Globalization;
using System.Text;
using Diten.BuildingBlocks.ListExport;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.ServerList;

/// <summary>
/// BL-452 package 1 — the shared list-export building block on its own: the column whitelist, the CSV writer's quoting
/// and formula neutralisation, the culture and format rules. The HTTP tests (GoldenCompactExportTests here, the Users
/// export tests in AuthService) measure it end to end; these pin the edges a seeded list does not reach.
/// </summary>
public sealed class ListExportContractTests
{
    private sealed record Row(string A, string B);

    private static readonly ListExportColumnSet<Row> Columns = new(
    [
        new("a", _ => "Col A", (r, _) => r.A),
        new("b", c => c.Name == "tr" ? "Sütun B" : "Col B", (r, _) => r.B)
    ]);

    private static string Csv(IEnumerable<string?>? requested, params Row[] rows)
    {
        Assert.True(Columns.TryResolve(requested, out var columns, out _));
        return Encoding.UTF8.GetString(ListExportWriter.WriteCsv(columns, rows, CultureInfo.GetCultureInfo("en")))[1..]; // drop the BOM
    }

    [Fact]
    public void Requested_columns_come_back_in_the_callers_order_once_each_repeated_or_comma_separated()
    {
        Assert.True(Columns.TryResolve(["b,a", "B"], out var columns, out _));
        Assert.Equal(new[] { "b", "a" }, columns.Select(c => c.Key));
    }

    [Fact]
    public void No_requested_column_means_every_declared_column_and_an_unknown_one_refuses_the_request()
    {
        Assert.True(Columns.TryResolve(null, out var all, out _));
        Assert.Equal(new[] { "a", "b" }, all.Select(c => c.Key));

        Assert.False(Columns.TryResolve(["a", "secret"], out var none, out var error));
        Assert.Empty(none);
        Assert.Contains("a, b", error);
    }

    [Fact]
    public void Csv_quotes_separators_quotes_and_line_breaks_per_rfc_4180()
    {
        var text = Csv(null, new Row("x, y", "say \"hi\"\r\nbye"));
        Assert.Equal("Col A,Col B\r\n\"x, y\",\"say \"\"hi\"\"\r\nbye\"\r\n", text);
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("@x", "'@x")]
    [InlineData("-x", "'-x")]
    [InlineData("\tx", "'\tx")]
    [InlineData("-5", "-5")]
    [InlineData("+3.5", "+3.5")]
    [InlineData("a=b", "a=b")]
    public void A_cell_a_spreadsheet_would_run_as_a_formula_is_written_as_text(string value, string written)
    {
        var line = Csv(["a"], new Row(value, "")).Split("\r\n")[1];
        Assert.Equal(written, line.Trim('"'));
    }

    [Fact]
    public void The_csv_starts_with_a_utf8_byte_order_mark()
    {
        Assert.True(Columns.TryResolve(null, out var columns, out _));
        var bytes = ListExportWriter.WriteCsv(columns, [new Row("ç", "ı")], CultureInfo.InvariantCulture);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("tr-TR,tr;q=0.9,en;q=0.8", "tr")]
    [InlineData("de-DE,fr;q=0.7", "fr")]
    [InlineData("en;q=0.2,zh-Hans;q=0.9", "zh")]
    [InlineData("ar-SA", "ar")]
    [InlineData("de,ja", "en")]
    [InlineData("ru;q=0", "en")]
    public void The_request_culture_is_the_best_ranked_tenant_language_else_english(string? header, string expected)
        => Assert.Equal(expected, ListExportContract.ResolveCulture(header).Name);

    [Theory]
    [InlineData(null, true, ListExportFormat.Csv)]
    [InlineData("CSV", true, ListExportFormat.Csv)]
    [InlineData("xlsx", true, ListExportFormat.Xlsx)]
    [InlineData("pdf", false, ListExportFormat.Csv)]
    public void Only_csv_and_xlsx_are_formats(string? value, bool ok, ListExportFormat expected)
    {
        Assert.Equal(ok, ListExportContract.TryParseFormat(value, out var format));
        Assert.Equal(expected, format);
    }

    [Fact]
    public void The_file_is_named_after_the_screen_and_the_utc_minute()
        => Assert.Equal("users-20260925-1405.xlsx",
            ListExportContract.FileName("users", new DateTimeOffset(2026, 9, 25, 17, 5, 59, TimeSpan.FromHours(3)), ListExportFormat.Xlsx));
}
