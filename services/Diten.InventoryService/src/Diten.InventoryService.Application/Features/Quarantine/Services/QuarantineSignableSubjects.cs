using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>ESIGN-BUNDLE §6 <c>SignableSubject</c> as 0175 answers it.</summary>
public sealed record SignableSubjectDto(string Display, IReadOnlyList<SignableSubjectLineDto> Summary, string? LegalEntityId, IReadOnlyDictionary<string, string?> Fields);

public sealed record SignableSubjectLineDto(string Label, string Value);

/// <summary>
/// MOD-0175 Q3 — the signable subject of a quarantine decision, field list <c>qd-v1</c> (pack §8, CT-D2: 0175's contract
/// obligation — names stable, versioned). Every value is a string or null: quantities invariant-culture with no trailing zeros,
/// dates UTC ISO-8601 "O", booleans <c>true</c> / <c>false</c>. Only facts fixed at draft time (the decision is insert-only)
/// and identifiers that do not change (hold / blocked number, lot number, location codes) enter the list — the workflow and
/// approval state never do, so the hash taken at signing still holds when the approver signs and afterwards.
/// <para><c>skuCode</c> is always null: PRODUCT-MASTER answers no SKU code (<c>getSku</c> returns ids only) —
/// ASSUMPTION-0175-Q3-03.</para>
/// </summary>
public sealed class QuarantineSignableSubjectBuilder
{
    /// <summary>The <c>qd-v1</c> field names (QUARANTINE-BUNDLE <c>SignableSubject.fields</c>, closed, 35 keys incl. <c>subjectVersion</c>).</summary>
    public static readonly IReadOnlyList<string> FieldNames =
    [
        "subjectVersion", "decisionNumber", "decisionKind", "holdNumber", "blockedNumber",
        "legalEntityId", "itemId", "skuId", "skuCode", "lotId", "lotNumber",
        "warehouseCode", "locationCode", "fromStockStatus",
        "line1.decision", "line1.quantity", "line1.toStockStatus", "line2.decision", "line2.quantity", "line2.toStockStatus",
        "baseUom", "blockReasonCode", "reason", "coaChecked", "coaReference",
        "dispositionKind", "dispositionQuantity", "supplierId", "returnDocumentNo", "destructionMethod", "destructionDate", "sampleLocationCode",
        "newRetestDate", "lotStatusTarget", "deciderUserId"
    ];

    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly ILocationRepository _locations;

    public QuarantineSignableSubjectBuilder(IQuarantineRepository repository, QuarantineFacts facts, ILocationRepository locations)
    {
        _repository = repository;
        _facts = facts;
        _locations = locations;
    }

    public async Task<SignableSubjectDto> BuildAsync(QuarantineDecision decision, CancellationToken ct)
    {
        var hold = decision.HoldId is { } holdId ? await _repository.FindHoldAsync(holdId, ct) : null;
        var blocked = decision.BlockedStockId is { } blockedId ? await _repository.FindBlockedAsync(blockedId, ct) : null;
        var lot = await _facts.LotAsync(decision.LotId, ct);
        var lines = decision.Lines.OrderBy(l => l.LineNo).ToList();
        var line1 = lines.ElementAtOrDefault(0);
        var line2 = lines.ElementAtOrDefault(1);
        var d = decision.Disposition;

        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["subjectVersion"] = QuarantineSignableActions.FieldListVersion,
            ["decisionNumber"] = decision.DecisionNumber,
            ["decisionKind"] = decision.Kind.ToString(),
            ["holdNumber"] = hold?.HoldNumber,
            ["blockedNumber"] = blocked?.BlockedNumber,
            ["legalEntityId"] = decision.LegalEntityId?.ToString("D"),
            ["itemId"] = decision.ItemId.ToString("D"),
            ["skuId"] = decision.SkuId.ToString("D"),
            ["skuCode"] = null,
            ["lotId"] = decision.LotId,
            ["lotNumber"] = decision.LotId is null ? null : lot?.LotNumber ?? decision.LotId,
            ["warehouseCode"] = await CodeAsync(decision.WarehouseId, ct),
            ["locationCode"] = await CodeAsync(decision.LocationId, ct),
            ["fromStockStatus"] = WireNames.ToWire(decision.FromStockStatus),
            ["line1.decision"] = line1?.Decision.ToString(),
            ["line1.quantity"] = line1 is null ? null : Quantity(line1.Quantity),
            ["line1.toStockStatus"] = line1 is null ? null : WireNames.ToWire(line1.ToStockStatus),
            ["line2.decision"] = line2?.Decision.ToString(),
            ["line2.quantity"] = line2 is null ? null : Quantity(line2.Quantity),
            ["line2.toStockStatus"] = line2 is null ? null : WireNames.ToWire(line2.ToStockStatus),
            ["baseUom"] = decision.BaseUomId,
            ["blockReasonCode"] = decision.BlockReasonCode,
            ["reason"] = decision.Reason,
            ["coaChecked"] = decision.CoaChecked ? "true" : "false",
            ["coaReference"] = decision.CoaReference,
            ["dispositionKind"] = d?.Kind.ToString(),
            ["dispositionQuantity"] = d is null ? null : Quantity(d.Quantity),
            ["supplierId"] = d?.SupplierId?.ToString("D"),
            ["returnDocumentNo"] = d?.ReturnDocumentNo,
            ["destructionMethod"] = d?.DestructionMethod,
            ["destructionDate"] = Date(d?.DestructionDate),
            ["sampleLocationCode"] = d?.SampleLocationId is { } sample ? await CodeAsync(sample, ct) : null,
            ["newRetestDate"] = Date(decision.NewRetestDate),
            ["lotStatusTarget"] = decision.LotStatusTarget,
            ["deciderUserId"] = decision.DeciderUserId.ToString("D")
        };

        var subject = hold?.HoldNumber ?? blocked?.BlockedNumber ?? decision.DecisionNumber;
        var lotLabel = fields["lotNumber"] ?? "—";
        var display = Cut($"Karantina kararı {decision.DecisionNumber} · {subject} · Lot {lotLabel} · {Verdict(decision)}", 300);
        var summary = new List<SignableSubjectLineDto>
        {
            new("Karar / Decision", decision.DecisionNumber),
            new("Kayıt / Record", subject),
            new("Lot", lotLabel),
            new("Depo / Warehouse", fields["warehouseCode"] ?? decision.WarehouseId),
            new("Lokasyon / Location", fields["locationCode"] ?? decision.LocationId),
            new("Stok durumu / Stock status", fields["fromStockStatus"]!)
        };
        foreach (var line in lines)
        {
            summary.Add(new($"Satır {line.LineNo} / Line {line.LineNo}",
                $"{line.Decision} · {Quantity(line.Quantity)} {decision.BaseUomId} → {WireNames.ToWire(line.ToStockStatus)}"));
        }

        if (d is not null)
        {
            summary.Add(new("Disposition", $"{d.Kind} · {Quantity(d.Quantity)} {decision.BaseUomId}"));
        }

        if (decision.BlockReasonCode is { } blockReason) summary.Add(new("Bloke nedeni / Block reason", blockReason));
        summary.Add(new("CoA kontrol edildi / CoA checked", decision.CoaChecked ? "Evet / Yes" : "Hayır / No"));
        summary.Add(new("Gerekçe / Reason", Cut(decision.Reason, 300)));
        if (decision.NewRetestDate is { } retest) summary.Add(new("Yeni yeniden test tarihi / New retest date", Date(retest)!));

        return new SignableSubjectDto(display, summary.Take(20).Select(s => s with { Label = Cut(s.Label, 60), Value = Cut(s.Value, 300) }).ToList(),
            decision.LegalEntityId?.ToString("D"), fields);
    }

    /// <summary>ESIGN-BUNDLE §7 canonical subject — byte-identical to MOD-0022's <c>ESignatureDigest.CanonicalSubject</c>.</summary>
    public static string Canonical(string subjectId, IReadOnlyDictionary<string, string?> fields)
    {
        var builder = new StringBuilder();
        builder.Append("{\"module\":").Append(Quote(QuarantineSignableActions.Module))
            .Append(",\"subjectType\":").Append(Quote(QuarantineSignableActions.SubjectType))
            .Append(",\"subjectId\":").Append(Quote(subjectId))
            .Append(",\"fields\":{");
        var first = true;
        foreach (var pair in fields.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(Quote(pair.Key)).Append(':').Append(pair.Value is null ? "null" : Quote(pair.Value));
        }

        return builder.Append("}}").ToString();
    }

    /// <summary>SHA-256 (lower-case hex) of <see cref="Canonical"/> — the <c>subjectHash</c> MOD-0022 stores at signing.</summary>
    public static string Hash(string subjectId, IReadOnlyDictionary<string, string?> fields)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(subjectId, fields)))).ToLowerInvariant();

    private static string Quote(string value) => "\"" + value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";

    private async Task<string?> CodeAsync(string? locationId, CancellationToken ct)
        => Guid.TryParse(locationId, out var id) && await _locations.FindAsync(id, ct) is { } location ? location.Code : null;

    public static string Quantity(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string? Date(DateTimeOffset? value) => value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static string Verdict(QuarantineDecision decision) => decision.Kind switch
    {
        DecisionKind.HoldDecision => string.Join(" + ", decision.Lines.OrderBy(l => l.LineNo).Select(l => $"{l.Decision} {Quantity(l.Quantity)}")),
        DecisionKind.Disposition => $"Disposition {decision.Disposition!.Kind} {Quantity(decision.Disposition.Quantity)}",
        _ => decision.Kind.ToString()
    };

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
