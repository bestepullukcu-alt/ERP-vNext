using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Diten.Web.Models.SupplyChain.Shipments;

/// <summary>
/// MOD-0183 r9 §24.2 Golden Compact create form (DL-3): what the browser sends. The company rides the query, never this body
/// (R-2). Not user inputs any more: <c>sourceModule</c> / <c>sourceType</c> (the adapter sends <c>MOD-0183</c> / <c>MANUAL</c>,
/// OD-183-3), <c>shipToReference</c> (the adapter writes the chosen account's address, OD-183-4), <c>lineNumber</c> (assigned
/// in order by the adapter) and <c>inventoryReferenceId</c> (unused in v1). <see cref="CustomerRef"/> is checked by the adapter,
/// so a missing or malformed value answers <c>INVALID_CUSTOMER_ACCOUNT</c>, not a model-binding error.
/// </summary>
public sealed class CreateShipmentViewModel
{
    [Required, MinLength(1)] public string? SourceDocumentId { get; set; }
    [Required, MinLength(1)] public string? WarehouseReferenceId { get; set; }
    public string? CustomerRef { get; set; }
    [Required] public DateTimeOffset PlannedShipAt { get; set; }
    [Required, MinLength(1)] public List<CreateShipmentLineViewModel>? Lines { get; set; }
    public DateTimeOffset? PlannedDeliverAt { get; set; }
}

public sealed class CreateShipmentLineViewModel
{
    [Required] public Guid ItemId { get; set; }
    [Required] public Guid SkuId { get; set; }
    [Required, RegularExpression("^(Gsku|Lsku|FinishedGood|Item)$")] public string? SkuLevel { get; set; }
    [Required, RegularExpression(@"^-?[0-9]+(\.[0-9]+)?$")] public string? Quantity { get; set; }
    [Required, MinLength(1)] public string? UomId { get; set; }
    public string? LotId { get; set; }
    [Required, MinLength(1)] public string? FromLocationId { get; set; }
    [MaxLength(1000)] public string? FefoBypassReason { get; set; }
}

/// <summary>The <c>createShipment</c> body the adapter sends (SHIPMENT-BUNDLE 3.5.0). Optional stock fields left out when
/// empty, so a line without them keeps the fingerprint it had before 3.4.0.</summary>
public sealed record ShipmentCreateWire(string SourceModule, string SourceType, string SourceDocumentId, string WarehouseReferenceId,
    string ShipToReference, string CustomerRef, DateTimeOffset PlannedShipAt, IReadOnlyList<ShipmentLineWire> Lines, DateTimeOffset? PlannedDeliverAt)
{
    public const string ManualSourceModule = "MOD-0183";
    public const string ManualSourceType = "MANUAL";

    /// <summary>Lines numbered <c>1, 2, 3</c> in the order the form lists them (SB-6).</summary>
    public static ShipmentCreateWire From(CreateShipmentViewModel model, string customerRef, string shipToReference) => new(
        ManualSourceModule, ManualSourceType, model.SourceDocumentId!.Trim(), model.WarehouseReferenceId!.Trim(), shipToReference, customerRef,
        model.PlannedShipAt,
        model.Lines!.Select((l, i) => new ShipmentLineWire((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), l.ItemId, l.SkuId,
            l.Quantity!, l.UomId!.Trim(), l.SkuLevel!, Blank(l.LotId), l.FromLocationId!.Trim(),
            // SB-8: the FEFO reason is sent unchanged (an empty one is left out); the contract's minLength applies to what the user typed.
            string.IsNullOrWhiteSpace(l.FefoBypassReason) ? null : l.FefoBypassReason)).ToList(),
        model.PlannedDeliverAt);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ShipmentLineWire(string LineNumber, Guid ItemId, Guid SkuId, string Quantity, string UomId, string SkuLevel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LotId,
    string FromLocationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FefoBypassReason);

/// <summary>
/// MOD-0183 r9 §24.1 customer rules shared by the picker, address and create adapters. Only name, code and the four address
/// fields of a CRM account ever leave these methods (SB-4); none of them is logged (SB-21).
/// </summary>
public static partial class ShipmentCustomer
{
    public const string InvalidAccountCode = "INVALID_CUSTOMER_ACCOUNT";
    public const string AddressMissingCode = "CUSTOMER_ADDRESS_MISSING";

    [System.Text.RegularExpressions.GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Pattern();

    /// <summary>The SHIPMENT-BUNDLE 3.5.0 <c>CustomerRef</c> pattern: a Guid in "D" format, lower case.</summary>
    public static bool IsCustomerRef(string? value) => value is not null && Pattern().IsMatch(value);

    /// <summary>
    /// The delivery destination (CT kararı 2026-11-03, SB-23): CountryRef, CityRef, DistrictRef, AddressLine in that order,
    /// each trimmed, empty ones dropped, joined with ", "; nothing cut. Null when all four are empty.
    /// </summary>
    public static string? Destination(CustomerAddress address)
    {
        var parts = new[] { address.CountryRef, address.CityRef, address.DistrictRef, address.AddressLine }
            .Select(x => x?.Trim()).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>The projection of a CRM <c>AccountDetailDto</c> (the <c>data</c> of <c>GET /api/crm/accounts/{id}</c>).</summary>
    public static CustomerAddress? ReadDetail(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var data = doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
            if (data.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            return new CustomerAddress(Text(data, "accountName"), Text(data, "accountCode"), Text(data, "countryRef"), Text(data, "cityRef"),
                Text(data, "districtRef"), Text(data, "addressLine"));
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>The picker rows of a CRM account list (<c>data.items</c>): id (sent as <c>customerRef</c>), name and code only.</summary>
    public static CustomerPage? ReadList(string body)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var data = root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != System.Text.Json.JsonValueKind.Object || !data.TryGetProperty("items", out var items)
                || items.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            var rows = new List<CustomerOption>();
            foreach (var item in items.EnumerateArray())
            {
                var id = Text(item, "id");
                if (!Guid.TryParse(id, out var parsed)) continue;
                rows.Add(new CustomerOption(parsed.ToString("D"), Text(item, "accountName"), Text(item, "accountCode")));
            }
            var total = data.TryGetProperty("total", out var t) && t.TryGetInt64(out var value) ? value : rows.Count;
            return new CustomerPage(rows, total);
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private static string? Text(System.Text.Json.JsonElement item, string name) =>
        item.ValueKind == System.Text.Json.JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString() : null;
}

/// <summary>What the address adapter returns to the browser: name, code and the four address fields, nothing else (SB-4).</summary>
public sealed record CustomerAddress(string? Name, string? Code, string? CountryRef, string? CityRef, string? DistrictRef, string? AddressLine);

/// <summary>One picker row. <see cref="Id"/> rides to <c>customerRef</c>; the page never shows it (SB-20).</summary>
public sealed record CustomerOption(string Id, string? Name, string? Code);

public sealed record CustomerPage(IReadOnlyList<CustomerOption> Items, long Total);

public sealed class TransitionShipmentViewModel
{
    [Required, RegularExpression("^(Planned|Dispatched|InTransit|Delivered|Exception|Closed|Cancelled)$")]
    public string? TargetStatus { get; set; }

    [Required] public DateTimeOffset OccurredAt { get; set; }
    public string? ReasonCode { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
}

public sealed class CaptureShipmentPodViewModel
{
    [Required, MinLength(1)] public string? RecipientName { get; set; }
    [Required] public DateTimeOffset ReceivedAt { get; set; }
    [Required, MinLength(1)] public List<string>? EvidenceReferenceIds { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
}

/// <summary>
/// MOD-0183 DL-4 server export of the shipments list (BL-452, owner decision Natig 2026-11-05): every matching row, not the
/// page, as CSV (RFC 4180, UTF-8 with BOM) or a minimal Office Open XML workbook written without a package (inline strings,
/// one sheet). Cells that a spreadsheet would read as a formula (=, +, -, @, tab, CR) are prefixed with an apostrophe.
/// </summary>
public static class ShipmentExport
{
    /// <summary>The most rows one export writes (50 pages of 200).</summary>
    public const int MaxRows = 10_000;

    public static string SafeCell(string? value)
    {
        var text = value ?? string.Empty;
        return text.Length > 0 && "=+-@\t\r".Contains(text[0]) ? "'" + text : text;
    }

    public static byte[] Csv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new System.Text.StringBuilder();
        void Line(IEnumerable<string?> cells) => builder.Append(string.Join(',', cells.Select(c => "\"" + SafeCell(c).Replace("\"", "\"\"") + "\""))).Append("\r\n");
        Line(headers);
        foreach (var row in rows) Line(row);
        return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public static byte[] Xlsx(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        static string X(string? value) => System.Security.SecurityElement.Escape(SafeCell(value)) ?? string.Empty;
        static string Column(int index) { var name = string.Empty; for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name; return name; }
        var sheet = new System.Text.StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        var r = 0;
        void Row(IEnumerable<string?> cells)
        {
            r++;
            sheet.Append("<row r=\"").Append(r).Append("\">");
            var c = 0;
            foreach (var cell in cells) sheet.Append("<c r=\"").Append(Column(c++)).Append(r).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(X(cell)).Append("</t></is></c>");
            sheet.Append("</row>");
        }
        Row(headers);
        foreach (var row in rows) Row(row);
        sheet.Append("</sheetData></worksheet>");
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            void Entry(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal).Open(), new System.Text.UTF8Encoding(false));
                writer.Write(content);
            }
            Entry("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Entry("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Entry("xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Shipments\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            Entry("xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return stream.ToArray();
    }
}
