using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>One typed-lot group (pack §11.5, K-D): the ledger transactions of one SKU that carry the same typed lot text.</summary>
/// <param name="TypedText">The text exactly as the ledger stores it (<c>InventoryTransaction.LotId</c>).</param>
/// <param name="FirstPostedAt">The earliest posting of the group — "an earlier row" of the K-D typo rule.</param>
public sealed record TypedLotGroup(
    string GroupId,
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    string TypedText,
    int TransactionCount,
    DateTimeOffset FirstPostedAt);

/// <summary>A registered lot a typed group may be linked to (same SKU).</summary>
public sealed record RegisteredLotRef(Guid LotId, Guid SkuId, SkuLevel SkuLevel, string LotNumber);

/// <summary>TRACE <c>ManualLotGroup.suggestion.kind</c>.</summary>
public enum LotMatchKind
{
    ExactLot,
    SimilarLot,
    SimilarGroup,
    None
}

/// <summary>
/// The suggestion of one group. <see cref="Note"/>: <c>exact</c> (same text), <c>case</c> (same after trim + upper-case),
/// <c>dash-space</c> (same after removing <c>-</c> and spaces — "Yazım farkı olabilir"), <c>new</c> / <c>new-invalid</c>
/// (no match; the normalised text is / is not a valid lot number).
/// </summary>
public sealed record LotMatchSuggestion(LotMatchKind Kind, Guid? LotId, string? GroupId, string Note);

/// <summary>
/// K-D suggestion rule of the typed-lot migration (pack §11.5, §17 "ManualLotMatcher (exact / case / dash-space / row
/// chain ≤ 3)"), pure:
/// <list type="number">
/// <item>same number ignoring case and leading / trailing spaces as a registered lot of the SKU → <see cref="LotMatchKind.ExactLot"/>;</item>
/// <item>same after removing <c>-</c> and spaces as a registered lot of the SKU → <see cref="LotMatchKind.SimilarLot"/> (L-001 / L001);</item>
/// <item>same after removing <c>-</c> and spaces as an EARLIER group of the SKU → <see cref="LotMatchKind.SimilarGroup"/>
/// pointing at the earliest such group (the chain head);</item>
/// <item>otherwise <see cref="LotMatchKind.None"/> — "Yeni lot".</item>
/// </list>
/// Group identity is deterministic (SKU + typed text) so a list and the later commit name the same group, and the order
/// ("earlier") is SKU, first posting, text.
/// </summary>
public static class ManualLotMatcher
{
    /// <summary>The longest typo chain a commit resolves (group → group → group → lot / new lot).</summary>
    public const int MaxTypoChain = 3;

    public static string GroupIdOf(Guid skuId, SkuLevel level, string typedText)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{skuId:D}|{level}|{typedText}"));
        return "mlg-" + Convert.ToHexString(hash, 0, 10).ToLowerInvariant();
    }

    /// <summary>Trim + upper-case — the registered lot number rule (<see cref="LotIdentityPolicy.NormalizeLotNumber"/>).</summary>
    public static string Normalize(string typedText) => LotIdentityPolicy.NormalizeLotNumber(typedText);

    /// <summary>The K-D typo key: normalised, without <c>-</c> and spaces.</summary>
    public static string Compact(string text) => Normalize(text).Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);

    public static IReadOnlyList<TypedLotGroup> Order(IEnumerable<TypedLotGroup> groups)
        => groups.OrderBy(g => g.SkuId).ThenBy(g => g.SkuLevel).ThenBy(g => g.FirstPostedAt).ThenBy(g => g.TypedText, StringComparer.Ordinal).ToList();

    public static IReadOnlyDictionary<string, LotMatchSuggestion> Suggest(IReadOnlyList<TypedLotGroup> groups, IReadOnlyList<RegisteredLotRef> lots)
    {
        var ordered = Order(groups);
        var result = new Dictionary<string, LotMatchSuggestion>(StringComparer.Ordinal);
        var lotsBySku = lots.GroupBy(l => (l.SkuId, l.SkuLevel)).ToDictionary(g => g.Key, g => g.OrderBy(l => l.LotNumber, StringComparer.Ordinal).ToList());
        var headBySkuAndKey = new Dictionary<(Guid, SkuLevel, string), string>();

        foreach (var group in ordered)
        {
            var normalized = Normalize(group.TypedText);
            var compact = Compact(group.TypedText);
            var skuLots = lotsBySku.TryGetValue((group.SkuId, group.SkuLevel), out var list) ? list : [];
            var key = (group.SkuId, group.SkuLevel, compact);

            if (skuLots.FirstOrDefault(l => string.Equals(l.LotNumber, normalized, StringComparison.Ordinal)) is { } exact)
            {
                result[group.GroupId] = new LotMatchSuggestion(LotMatchKind.ExactLot, exact.LotId, null,
                    string.Equals(group.TypedText, exact.LotNumber, StringComparison.Ordinal) ? "exact" : "case");
            }
            else if (compact.Length > 0 && skuLots.FirstOrDefault(l => string.Equals(Compact(l.LotNumber), compact, StringComparison.Ordinal)) is { } similar)
            {
                result[group.GroupId] = new LotMatchSuggestion(LotMatchKind.SimilarLot, similar.LotId, null, "dash-space");
            }
            else if (compact.Length > 0 && headBySkuAndKey.TryGetValue(key, out var head))
            {
                result[group.GroupId] = new LotMatchSuggestion(LotMatchKind.SimilarGroup, null, head, "dash-space");
            }
            else
            {
                result[group.GroupId] = new LotMatchSuggestion(LotMatchKind.None, null, null,
                    LotIdentityPolicy.IsValidLotNumber(normalized) ? "new" : "new-invalid");
            }

            if (compact.Length > 0)
            {
                headBySkuAndKey.TryAdd(key, group.GroupId);
            }
        }

        return result;
    }
}
