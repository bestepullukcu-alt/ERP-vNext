using System.Text.RegularExpressions;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders.Validators;

// WP-VW-W6 W6-BE-a — MOD-0168 §13.1 field rules. Each answers (code, message) of the first problem, or null.

public static class PreOrderValidator
{
    /// <summary>A draft (create / update). Lines may be empty on a draft; the submission needs ≥ 1 (§13.1).</summary>
    public static (string Code, string Message)? Check(PreOrderInput? input, bool create, DateOnly today)
    {
        if (input is null)
        {
            return (PreOrderErrors.Invalid, "A body is required.");
        }

        if (create && (input.PharmacyAccountId is null || input.PharmacyAccountId == Guid.Empty))
        {
            return (PreOrderErrors.PharmacyInvalid, "The pharmacy is required.");
        }

        if (input.DepotAccountId == Guid.Empty)
        {
            return (PreOrderErrors.DepotInvalid, "The depot id is not valid.");
        }

        if (input.RequestedDeliveryDate is { } day && day < today)
        {
            return (PreOrderErrors.DeliveryDateInvalid, "The delivery date is today or later.");
        }

        if (input.Note is { Length: > PreOrderLimits.NoteMax })
        {
            return (PreOrderErrors.NoteTooLong, $"The note is at most {PreOrderLimits.NoteMax} characters.");
        }

        return input.Lines is null ? null : CheckLines(input.Lines);
    }

    public static (string Code, string Message)? CheckLines(IReadOnlyList<PreOrderLineInput> lines)
    {
        if (lines.Count > PreOrderLimits.MaxLines)
        {
            return (PreOrderErrors.LinesInvalid, $"At most {PreOrderLimits.MaxLines} lines.");
        }

        var seen = new HashSet<Guid>();
        foreach (var line in lines)
        {
            if (line is null || line.SkuId is not { } sku || sku == Guid.Empty)
            {
                return (PreOrderErrors.LinesInvalid, "Every line names its SKU.");
            }

            if (!seen.Add(sku))
            {
                return (PreOrderErrors.LinesInvalid, "The same SKU cannot be on two lines.");
            }

            if (line.Quantity is not { } quantity || quantity < 1)
            {
                return (PreOrderErrors.LinesInvalid, "A line's quantity is a whole number of at least 1.");
            }

            if (line.Mf is { } mf && (mf.Buy < 1 || mf.Free < 1))
            {
                return (PreOrderErrors.MfInvalid, "An MF is a + b with a, b ≥ 1 (e.g. 10+2).");
            }

            if (line.DiscountPct is { } discount && (discount < 0m || discount >= 100m || decimal.Round(discount, 1) != discount))
            {
                return (PreOrderErrors.DiscountInvalid, "A discount is 0 ≤ x < 100 with at most one decimal.");
            }
        }

        return null;
    }

    public static (string Code, string Message)? CheckDecision(PreOrderDecisionInput? input, out string decision, out string? reason)
    {
        decision = input?.Decision?.Trim().ToLowerInvariant() ?? string.Empty;
        reason = PreOrderResults.Clean(input?.Reason);
        if (!PreOrderDecisions.All.Contains(decision))
        {
            return (PreOrderErrors.DecisionInvalid, "The decision is approve, reject or request-changes.");
        }

        if (decision != PreOrderDecisions.Approve && reason is null)
        {
            return (PreOrderErrors.ReasonRequired, "Rejecting or requesting changes needs a reason.");
        }

        return reason is { Length: > PreOrderLimits.ReasonMax } ? (PreOrderErrors.Invalid, $"The reason is at most {PreOrderLimits.ReasonMax} characters.") : null;
    }

    public static (string Code, string Message)? CheckReason(string? text, out string? reason)
    {
        reason = PreOrderResults.Clean(text);
        if (reason is null)
        {
            return (PreOrderErrors.ReasonRequired, "A reason is required.");
        }

        return reason.Length > PreOrderLimits.ReasonMax ? (PreOrderErrors.Invalid, $"The reason is at most {PreOrderLimits.ReasonMax} characters.") : null;
    }

    public static (string Code, string Message)? CheckDepotResponse(PreOrderDepotResponseInput? input, PreOrder order, out string status)
    {
        status = input?.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!PreOrderDepotResponseStatuses.All.Contains(status))
        {
            return (PreOrderErrors.DepotResponseInvalid, "The depot's answer is delivered or partial.");
        }

        if (input!.Note is { Length: > PreOrderLimits.ReasonMax })
        {
            return (PreOrderErrors.DepotResponseInvalid, $"The note is at most {PreOrderLimits.ReasonMax} characters.");
        }

        if (status == PreOrderDepotResponseStatuses.Delivered)
        {
            return input.PartialLines is { Count: > 0 } ? (PreOrderErrors.DepotResponseInvalid, "A full delivery has no partial lines.") : null;
        }

        if (input.PartialLines is not { Count: > 0 } partial)
        {
            return (PreOrderErrors.DepotResponseInvalid, "A partial delivery names at least one line.");
        }

        var seen = new HashSet<int>();
        foreach (var p in partial)
        {
            var line = order.Lines.FirstOrDefault(l => l.LineNo == p.LineNo);
            if (line is null || !seen.Add(p.LineNo) || p.DeliveredQuantity < 0 || p.DeliveredQuantity >= line.Quantity)
            {
                return (PreOrderErrors.DepotResponseInvalid, "Each partial line exists once, with 0 ≤ delivered < ordered.");
            }
        }

        return null;
    }

    public static (string Code, string Message)? CheckSignature(PreOrderSignatureInput? input, out byte[] image, out string mediaType)
    {
        image = Array.Empty<byte>();
        mediaType = input?.MediaType?.Trim().ToLowerInvariant() ?? string.Empty;
        var signer = PreOrderResults.Clean(input?.SignerName);
        if (signer is null || signer.Length > PreOrderLimits.SignerNameMax)
        {
            return (PreOrderErrors.SignatureInvalid, $"The signer's name is required (≤ {PreOrderLimits.SignerNameMax} characters).");
        }

        if (mediaType is not ("image/png" or "image/jpeg"))
        {
            return (PreOrderErrors.SignatureInvalid, "The signature is a PNG or JPEG image.");
        }

        var text = input!.ImageBase64?.Trim() ?? string.Empty;
        var comma = text.IndexOf(',');
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            text = text[(comma + 1)..];
        }

        try
        {
            image = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return (PreOrderErrors.SignatureInvalid, "The signature image is not valid base64.");
        }

        return image.Length is 0 or > (int)PreOrderLimits.SignatureMaxBytes
            ? (PreOrderErrors.SignatureInvalid, "The signature image is required and at most 10 MB.")
            : null;
    }
}

public static partial class PreOrderSettingsValidator
{
    [GeneratedRegex("^[A-Z]{2,5}$")]
    private static partial Regex PrefixPattern();

    public static (string Code, string Message)? Check(PreOrderSettingsInput? input)
    {
        if (input is null)
        {
            return (PreOrderErrors.SettingsInvalid, "A body is required.");
        }

        if (input.ApprovalMode is { } mode && !PreOrderApprovalModes.All.Contains(mode.Trim().ToLowerInvariant()))
        {
            return (PreOrderErrors.SettingsInvalid, "The approval mode is always or auto-within-limits.");
        }

        if (input.ApprovalLinkTtlHours is { } ttl && (ttl < PreOrderSettingsDefaults.ApprovalLinkTtlMin || ttl > PreOrderSettingsDefaults.ApprovalLinkTtlMax))
        {
            return (PreOrderErrors.SettingsInvalid, "The link lives 12–168 hours.");
        }

        if (input.TransmissionRetryMax is { } retries && (retries < 0 || retries > PreOrderSettingsDefaults.TransmissionRetryLimit))
        {
            return (PreOrderErrors.SettingsInvalid, "Transmission retries are 0–10.");
        }

        if (input.PriceTierByCountry is { } tiers)
        {
            var countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tier in tiers)
            {
                if (tier is null || string.IsNullOrWhiteSpace(tier.CountryCode) || tier.CountryCode.Trim().Length != 2 || !countries.Add(tier.CountryCode.Trim())
                    || !PriceTiers.All.Contains(tier.Tier?.Trim().ToLowerInvariant() ?? string.Empty))
                {
                    return (PreOrderErrors.SettingsInvalid, "Each country (ISO 3166-1 alpha-2) appears once with a tier: depot, pharmacist-purchase or public.");
                }
            }
        }

        if (input.CompanyPrefixes is { } prefixes)
        {
            var entities = new HashSet<Guid>();
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in prefixes)
            {
                if (p?.LegalEntityId is not { } le || le == Guid.Empty || !entities.Add(le) || p.Prefix is null
                    || !PrefixPattern().IsMatch(p.Prefix.Trim()) || !codes.Add(p.Prefix.Trim()))
                {
                    return (PreOrderErrors.SettingsInvalid, "Each legal entity appears once with its own prefix of 2–5 letters A–Z.");
                }
            }
        }

        return null;
    }
}
