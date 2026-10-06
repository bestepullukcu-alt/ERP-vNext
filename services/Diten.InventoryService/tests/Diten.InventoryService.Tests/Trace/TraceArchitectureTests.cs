using System.Reflection;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Traceability;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Persistence.Traceability;
using Diten.InventoryService.Tests.Architecture;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0174 architecture guards (pack §8, §16 P1, §17), measured on the shipping sources and types:
/// <list type="bullet">
/// <item>K-G: ONLY <c>LotIdentityPolicy</c> builds a lot / serial key — every <c>IdentityKey =</c> and every identity
/// lookup takes the policy's value, and no other code filters lots by number equality;</item>
/// <item>no delete: no repository member, driver call or route can remove a lot, a history row, a serial or a link;</item>
/// <item>no HTTP self-call: no production code but the controller route and the contract constant names
/// <c>api/trace</c> (the ledger links in-process from slice P2);</item>
/// <item>no Model B; and P1 leaves the MOD-0173 ledger feature untouched (it does not even reference Traceability).</item>
/// </list>
/// </summary>
public sealed class TraceArchitectureTests
{
    private static readonly Regex Mutating = new(@"^(Delete|Remove|Purge|Drop|Erase)", RegexOptions.Compiled);

    private static IReadOnlyList<SourceScan.SourceFile> TraceSources()
        => SourceScan.Production().Where(f => f.RelativePath.Contains("/Traceability/", StringComparison.Ordinal)).ToList();

    [Fact]
    public void Only_LotIdentityPolicy_builds_a_lot_or_serial_key()
    {
        var production = SourceScan.Production().Where(f => !f.RelativePath.EndsWith("/LotIdentityPolicy.cs", StringComparison.Ordinal)).ToList();
        Assert.Contains(production, f => f.Body.Contains("IdentityKey = LotIdentityPolicy.", StringComparison.Ordinal)); // non-vacuous

        var offenders = production
            .SelectMany(f => f.Body.Split('\n').Select(l => (f.RelativePath, Line: l.Trim())))
            .Where(x => Regex.IsMatch(x.Line, @"\bIdentityKey\s*=(?!=)") && !x.Line.Contains("LotIdentityPolicy.", StringComparison.Ordinal))
            .Concat(production.SelectMany(f => f.Body.Split('\n').Select(l => (f.RelativePath, Line: l.Trim())))
                .Where(x => x.Line.Contains("FindByIdentityKeyAsync(", StringComparison.Ordinal)
                            && !x.Line.Contains("Task<", StringComparison.Ordinal)
                            && !x.Line.Contains("LotIdentityPolicy.", StringComparison.Ordinal)))
            .Concat(production.SelectMany(f => f.Body.Split('\n').Select(l => (f.RelativePath, Line: l.Trim())))
                .Where(x => Regex.IsMatch(x.Line, @"Filter\.(Eq|In)\(x => x\.(LotNumber|Number)\b")))
            .Select(x => $"{x.RelativePath}: {x.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "lot / serial key built outside LotIdentityPolicy:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Nothing_can_delete_a_lot_a_history_row_a_serial_or_a_genealogy_link()
    {
        Type[] stores = [typeof(ILotRepository), typeof(ILotTransaction), typeof(ISerialRepository), typeof(ILotExpirySweeper),
            typeof(LotRepository), typeof(SerialRepository), typeof(LotExpirySweeper)];
        var members = stores.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(m => $"{t.Name}.{m.Name}")).ToList();
        Assert.DoesNotContain(members, m => Mutating.IsMatch(m[(m.IndexOf('.') + 1)..]));

        var driverDeletes = TraceSources()
            .Where(f => Regex.IsMatch(f.Body, @"\.(DeleteOne|DeleteMany|FindOneAndDelete|DropCollection)\w*\s*\("))
            .Select(f => f.RelativePath)
            .ToList();
        Assert.True(driverDeletes.Count == 0, "delete call in: " + string.Join(", ", driverDeletes));

        var routes = typeof(TraceController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .ToHashSet();
        Assert.Equal(["GET", "PATCH", "POST"], routes.OrderBy(x => x));
    }

    [Fact]
    public void No_production_code_calls_api_trace_over_HTTP()
    {
        var offenders = SourceScan.Production()
            .Where(f => f.Body.Contains("api/trace", StringComparison.Ordinal))
            .Select(f => f.RelativePath)
            .Where(p => !p.EndsWith("/TraceController.cs", StringComparison.Ordinal) && !p.EndsWith("/TraceContract.cs", StringComparison.Ordinal))
            .ToList();
        Assert.True(offenders.Count == 0, "api/trace named in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_trace_code_never_touches_Model_B_and_the_ledger_feature_does_not_reference_it()
    {
        Assert.All(TraceSources(), f =>
        {
            Assert.DoesNotContain("mdm_products", f.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("api/mdm/products", f.Body, StringComparison.Ordinal);
        });

        // WP-0174-P2: the ledger feature reaches the lot master ONLY through the pack §5 "P2 only" hook points — the
        // validator (lot / serial checks), the posting handler (context + refusal) and getAvailability (CT-D1). No other
        // ledger feature file references Traceability.
        string[] hooks = ["/Services/MovementMasterDataValidator.cs", "/CommandHandlers/PostMovementHandler.cs", "/QueryHandlers/GetAvailabilityHandler.cs"];
        var ledger = SourceScan.Production().Where(f => f.RelativePath.Contains("/Features/Inventory/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(ledger);
        Assert.All(hooks, h => Assert.Contains(ledger, f => f.RelativePath.EndsWith(h, StringComparison.Ordinal) && f.Body.Contains("Traceability", StringComparison.Ordinal)));
        Assert.All(ledger.Where(f => !hooks.Any(h => f.RelativePath.EndsWith(h, StringComparison.Ordinal))),
            f => Assert.DoesNotContain("Traceability", f.Body, StringComparison.Ordinal));
    }
}
