using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-E4R2-FIX-d items (7) and (8), from the FIX-c acceptance, on the REAL page script (index.js +
/// thresholds-core.js) run in Node:
/// (7) the tenant default is the FIRST row whatever the reader sorts by — orderFixed.pre [[0, 'asc']] over the control
///     column's sort value (0 the default, 1 a product), ordered the way DataTables builds it (the fixed order BEFORE the
///     reader's column) for every sortable column, both directions;
/// (8) the medium band "a–b" is one left-to-right isolate (LRI…PDI), so Arabic never reads "20–5"; the digits stay the
///     culture's.
/// </summary>
public sealed partial class VwW4PharmacyThresholdsWebTests
{
    private const char Lri = '\u2066';
    private const char Pdi = '\u2069';

    [Fact]
    public void FixD7_the_tenant_default_is_the_first_row_for_every_sortable_column_in_both_directions()
    {
        var r = Smoke("potential-thresholds-smoke.js", PotentialThresholdsIndexAllKeys(), "PotentialThresholds", "PotentialThresholdsIndex", "manage");
        if (r is null) { return; }
        var o = r.Value;
        Assert.Equal("[[0,\"asc\"]]", o.GetProperty("orderFixedPre").GetRawText());
        // the control column's sort value: 0 for the default, 1 for a product
        var sortValues = o.GetProperty("controlSortValues").EnumerateArray().ToDictionary(x => x[0].GetString()!, x => x[1].GetInt32());
        Assert.Equal(0, sortValues["default"]);
        Assert.All(sortValues.Where(x => x.Key != "default"), x => Assert.Equal(1, x.Value));
        Assert.True(o.GetProperty("rowCount").GetInt32() >= 2);

        var firsts = o.GetProperty("firstByOrder").EnumerateObject().ToList();
        Assert.Contains(firsts, x => x.Name == "product:desc");                   // the name column is sortable, both ways
        Assert.Contains(firsts, x => x.Name == "lowBelow:desc");
        Assert.All(firsts, x => Assert.True(x.Value.GetString() == "default", x.Name + " put " + x.Value.GetString() + " first"));
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("tr")]
    public void FixD8_the_medium_band_range_is_one_left_to_right_isolate(string culture)
    {
        var r = Smoke("potential-thresholds-smoke.js", PotentialThresholdsIndexAllKeys(), "PotentialThresholds", "PotentialThresholdsIndex", "manage", culture);
        if (r is null) { return; }
        var o = r.Value;
        // the list's medium cell: <span class="pt-band pt-band-medium">\u20665–20\u2069</span>
        var cells = Regex.Matches(o.GetProperty("rows").GetString()!, "<span class=\"pt-band pt-band-medium\">([^<]*)</span>").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(cells);
        Assert.All(cells, c => Assert.Matches("^" + Lri + "[^" + Pdi + "]*–[^" + Pdi + "]*" + Pdi + "$", c));
        Assert.Contains(Lri + "5–20" + Pdi, cells);                                  // the tenant default 5–20, in order
        // the edit drawer's band preview (the localized "Medium {0}–{1}"): the range isolated inside the template
        var bands = o.GetProperty("bands").GetString()!;
        Assert.Contains(Lri + "50–60" + Pdi, bands);
    }

    [Fact]
    public void FixD8_the_core_isolates_latin_and_arabic_indic_ranges_and_nothing_else()
    {
        var crm = Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM").Replace("\\", "/");
        var node = RunNode("const c = require('" + crm + "/PotentialThresholds/thresholds-core.js'); process.stdout.write(JSON.stringify(["
                           + "c.isolateRange('متوسط 5–20'), c.isolateRange('٥–٢٠'), c.isolateRange('1.000 – 20.000'), c.isolateRange('5'), c.isolateRange(null)]));");
        if (node is null) { return; }
        var v = System.Text.Json.JsonSerializer.Deserialize<string[]>(node)!;
        Assert.Equal("متوسط " + Lri + "5–20" + Pdi, v[0]);
        Assert.Equal(Lri + "٥–٢٠" + Pdi, v[1]);                                     // Arabic-Indic digits stay
        Assert.Equal(Lri + "1.000 – 20.000" + Pdi, v[2]);
        Assert.Equal("5", v[3]);
        Assert.Equal(string.Empty, v[4]);
        // the page uses it on both band texts (the list cell and the drawer / quick view bands)
        var index = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "CRM", "PotentialThresholds", "index.js"));
        Assert.Equal(2, Regex.Matches(index, @"C\.isolateRange\(").Count);
    }
}
