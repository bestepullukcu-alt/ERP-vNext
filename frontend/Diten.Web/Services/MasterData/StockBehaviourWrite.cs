using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Services.MasterData;

/// <summary>
/// MOD-0290-FU04 S5 (O-12) — who may edit a level's stock behaviour, as the screen draws it and its proxy checks it.
/// ⚠ Display and proxy only: MDM's [HasPermission(StockBehaviourPermissions.*)] stays the authority.
/// </summary>
public static class StockBehaviourAccess
{
    // O-12: own key after the Auth role-matrix round (BL-573) — today the key that edits the GSKU's identity (CT decision).
    public const string Gsku = "mdm.gskus.update";

    // O-12: own key after the Auth role-matrix round (BL-573) — today the LSKU's create key (CT decision).
    public const string Lsku = "mdm.lskus.create";

    // O-12: own key after the Auth role-matrix round (BL-573) — today the Finished Good's create key (CT decision).
    public const string FinishedGood = "mdm.finished-goods.create";
}

/// <summary>
/// MOD-0290-FU04 S5 — the stock-behaviour edit forms of the GSKU / LSKU / Finished Good screens, read the way every
/// lifecycle form of these proxies is read: only the level's own fields, each at most once, every value shape-checked
/// (MDM stays the judge of the rules — unit and condition codes are checked against its lists there, decision (b)); a
/// field left out is "not changed" and is not sent. The JSON MDM receives is built here in a fixed order, so the same
/// form attempt with the same values is the same request and derives the same Idempotency-Key (the GSKU lifecycle's
/// deterministic operation id: tenant · actor · record · action · version · payload, plus the form attempt).
/// </summary>
public static class StockBehaviourWrite
{
    public const string BaseUomCode = "BaseUomCode";
    public const string UomConversions = "UomConversions";
    public const string ShelfLifeDays = "ShelfLifeDays";
    public const string RetestDays = "RetestDays";
    public const string MinRemainingShelfLifeDays = "MinRemainingShelfLifeDays";
    public const string StorageConditionCodes = "StorageConditionCodes";
    public const string LotControlled = "LotControlled";
    public const string SerialControlled = "SerialControlled";
    public const string Stockable = "Stockable";
    public const string Gtin = "Gtin";

    // Each level's fields in the order they are sent (pack §4.2; MDM's request records).
    public static readonly IReadOnlyList<string> GskuFields =
    [
        BaseUomCode, UomConversions, ShelfLifeDays, StorageConditionCodes, LotControlled, Stockable, RetestDays,
        MinRemainingShelfLifeDays
    ];

    // O-3: the LSKU carries only its serial flag — and must send it.
    public static readonly IReadOnlyList<string> LskuFields = [SerialControlled];

    // O-14: no base unit (the GSKU's); the GTIN has its own request.
    public static readonly IReadOnlyList<string> FinishedGoodFields =
        [UomConversions, ShelfLifeDays, StorageConditionCodes, LotControlled, SerialControlled, Stockable];

    public static readonly IReadOnlyList<string> GtinFields = [Gtin];

    private const int MaxListItems = 32;
    private const int MaxDays = 99_999;
    private static readonly Regex Code = new("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex Amount = new(@"^[0-9]{1,18}(\.[0-9]{1,18})?$", RegexOptions.CultureInvariant);
    private static readonly Regex Gtin14 = new("^[0-9]{14}$", RegexOptions.CultureInvariant);

    /// <param name="Body">The JSON sent to MDM (expectedVersion + the changed fields, camelCase, fixed order).</param>
    public sealed record Request(int ExpectedVersion, Guid FormAttemptId, string Body);

    /// <summary>
    /// The form, or null when it is not one this level accepts (an unknown or repeated field, a malformed value, no
    /// change at all, or a required field missing). <paramref name="required"/> must all be present.
    /// </summary>
    public static async Task<Request?> ReadAsync(
        HttpRequest request, IReadOnlyList<string> fields, IReadOnlyCollection<string> required, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType) return null;
        var form = await request.ReadFormAsync(cancellationToken);
        var allowed = new HashSet<string>(fields, StringComparer.Ordinal)
        {
            "ExpectedVersion", "FormAttemptId", "__RequestVerificationToken"
        };
        if (form.Keys.Any(key => !allowed.Contains(key)) || form.Any(pair => pair.Value.Count != 1)
            || !form.ContainsKey("__RequestVerificationToken")
            || !int.TryParse(form["ExpectedVersion"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || !Guid.TryParseExact(form["FormAttemptId"].ToString(), "D", out var attempt) || attempt == Guid.Empty
            || required.Any(field => !form.ContainsKey(field)))
        {
            return null;
        }

        var body = new JsonObject { ["expectedVersion"] = version };
        foreach (var field in fields)
        {
            if (!form.TryGetValue(field, out var raw)) continue;
            var value = Parse(field, raw.ToString().Trim());
            if (value is null) return null;
            body[char.ToLowerInvariant(field[0]) + field[1..]] = value;
        }
        // A request that changes nothing is MDM's 400 too — refused here without the round trip.
        if (body.Count == 1) return null;
        return new Request(version, attempt, body.ToJsonString());
    }

    private static JsonNode? Parse(string field, string value) => field switch
    {
        BaseUomCode => CodeOf(value) is { } code ? JsonValue.Create(code) : null,
        UomConversions => ConversionsOf(value),
        StorageConditionCodes => CodesOf(value),
        ShelfLifeDays or RetestDays or MinRemainingShelfLifeDays =>
            value.Length is > 0 and <= 5 && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var days)
            && days <= MaxDays ? JsonValue.Create(days) : null,
        LotControlled or SerialControlled or Stockable => value switch
        {
            "true" => JsonValue.Create(true),
            "false" => JsonValue.Create(false),
            _ => null
        },
        Gtin => Gtin14.IsMatch(value) ? JsonValue.Create(value) : null,
        _ => null
    };

    private static string? CodeOf(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        return Code.IsMatch(code) ? code : null;
    }

    // Decision (b): storage conditions are a code input — "COOL_2_8, PROTECT_FROM_LIGHT". The whole list is sent (MDM
    // keeps every stored code; a missing one on an approved record is its 409).
    private static JsonArray? CodesOf(string value)
    {
        var codes = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(CodeOf).ToList();
        if (codes.Count is 0 or > MaxListItems || codes.Any(code => code is null)
            || codes.Distinct(StringComparer.Ordinal).Count() != codes.Count)
            return null;
        return new JsonArray(codes.Select(code => (JsonNode?)JsonValue.Create(code)).ToArray());
    }

    // The conversions as the screen holds them: [{ fromUom, toUom, numerator, denominator }] — the stored rows and the
    // new ones, whole. Amounts stay text as typed (MDM stores them unrounded and judges them, pack §4.4).
    private static JsonArray? ConversionsOf(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array
                || document.RootElement.GetArrayLength() is 0 or > MaxListItems)
                return null;
            var rows = new JsonArray();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || row.EnumerateObject().Count() != 4) return null;
                string? Text(string name) => row.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String
                    ? item.GetString()!.Trim() : null;
                var from = CodeOf(Text("fromUom") ?? string.Empty);
                var to = CodeOf(Text("toUom") ?? string.Empty);
                var numerator = Text("numerator");
                var denominator = Text("denominator");
                if (from is null || to is null || numerator is null || denominator is null
                    || !Amount.IsMatch(numerator) || !Amount.IsMatch(denominator))
                    return null;
                rows.Add(new JsonObject
                {
                    ["fromUom"] = from, ["toUom"] = to, ["numerator"] = numerator, ["denominator"] = denominator
                });
            }
            return rows;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The request's Idempotency-Key: the GSKU lifecycle's derivation (SHA-256 over length-prefixed parts, the first
    /// 16 bytes as a "D" GUID). The same form attempt sending the same values → the same key (a lost answer repeated is
    /// MDM's replay); other values or a new attempt → a new key.
    /// </summary>
    public static Guid OperationId(Guid tenantId, string actor, string level, string action, Guid recordId, Request request)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var part in new[]
                 {
                     tenantId.ToString("D"), actor, level, action, recordId.ToString("D"),
                     request.ExpectedVersion.ToString(CultureInfo.InvariantCulture),
                     $"{request.FormAttemptId:D}\n{request.Body}"
                 })
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            Span<byte> length = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        var hex = Convert.ToHexString(hash.GetHashAndReset().AsSpan(0, 16));
        return Guid.ParseExact($"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}", "D");
    }

    /// <summary>The signed-in human and their single tenant (the lifecycle proxies' rule), or false.</summary>
    public static bool TryResolveActor(ClaimsPrincipal user, out Guid tenantId, out string actor)
    {
        tenantId = Guid.Empty;
        actor = string.Empty;
        string[] Values(string type) => user.Claims.Where(claim => claim.Type == type).Select(claim => claim.Value).ToArray();
        var actorTypes = Values("actor_type");
        if (user.Identity?.IsAuthenticated != true || actorTypes.Length != 1
            || actorTypes[0] is not ("tenant_user" or "platform_admin" or "partner_admin"))
            return false;
        var subjects = Values("sub").Concat(Values(ClaimTypes.NameIdentifier)).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tenants = user.Claims.Where(claim => claim.Type is "tenantId" or "tenant_id"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (subjects.Length != 1 || !Guid.TryParseExact(subjects[0], "D", out var subject) || subject == Guid.Empty
            || tenants.Length != 1 || !Guid.TryParseExact(tenants[0], "D", out tenantId) || tenantId == Guid.Empty)
        {
            tenantId = Guid.Empty;
            return false;
        }
        actor = subject.ToString("D");
        return true;
    }

    /// <summary>The status a proxy answers with: MDM's own when it is one the screen reads, 422 included (D-17).</summary>
    public static int NormalizedStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
        HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
        HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
        HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
        HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
        HttpStatusCode.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
        HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
        HttpStatusCode.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway
    };

    /// <summary>
    /// Sends one stock-behaviour write (LSKU / Finished Good proxies; the GSKU proxy uses its own lifecycle sender) and
    /// answers the screen: MDM's 200 envelope as it is, or a refusal said in sentences through the GSKU code bridge
    /// (GskuRefusalSentences — every stock code MDM can answer is there). A code with no sentence is the status's
    /// sentence, logged and flagged (unmapped) — the raw code never reaches the browser.
    /// </summary>
    public static async Task<IActionResult> SendAsync(
        HttpClient http,
        HttpRequestMessage request,
        IStringLocalizer refusals,
        Func<int, string> statusSentence,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using (request)
            using (var response = await http.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await RefusalAsync(response, refusals, statusSentence, logger, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode != HttpStatusCode.OK
                    || response.Content.Headers.ContentType?.MediaType is not "application/json" || !IsSuccessEnvelope(body))
                    return Answer(StatusCodes.Status502BadGateway, [statusSentence(StatusCodes.Status502BadGateway)], false);
                return new ContentResult { StatusCode = StatusCodes.Status200OK, ContentType = "application/json", Content = body };
            }
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Stock-behaviour proxy timed out.");
            return Answer(StatusCodes.Status504GatewayTimeout, [statusSentence(StatusCodes.Status504GatewayTimeout)], false);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError("Stock-behaviour proxy failed: {FailureType}.", exception.GetType().Name);
            return Answer(StatusCodes.Status503ServiceUnavailable, [statusSentence(StatusCodes.Status503ServiceUnavailable)], false);
        }
    }

    private static bool IsSuccessEnvelope(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && (root.TryGetProperty("isSuccessful", out var successful) || root.TryGetProperty("IsSuccessful", out successful))
                && successful.ValueKind == JsonValueKind.True
                && (root.TryGetProperty("statusCode", out var status) || root.TryGetProperty("StatusCode", out status))
                && status.TryGetInt32(out var code) && code == StatusCodes.Status200OK;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<IActionResult> RefusalAsync(
        HttpResponseMessage response,
        IStringLocalizer refusals,
        Func<int, string> statusSentence,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var status = NormalizedStatus(response.StatusCode);
        var codes = await CodesAsync(response, cancellationToken);
        if (codes is null) return Answer(status, [statusSentence(status)], false);
        var sentences = new List<string>();
        var unmapped = false;
        foreach (var code in codes)
        {
            // A sentence that names fields ({0}) belongs to a GSKU-only refusal with its own labels — not one a stock
            // write of this level answers; said as the status if it ever comes.
            var sentence = GskuRefusalSentences.Keys.TryGetValue(code, out var key)
                           && refusals[key] is { ResourceNotFound: false } found
                           && !found.Value.Contains("{0}", StringComparison.Ordinal)
                ? found.Value
                : null;
            if (sentence is null)
            {
                unmapped = true;
                logger.LogWarning("Stock-behaviour proxy: MDM answered {Status} with a code that has no sentence ({ErrorCode}).",
                    status, IsCode(code) ? code : "UNREADABLE_CODE");
                sentence = statusSentence(status);
            }
            if (!sentences.Contains(sentence, StringComparer.Ordinal)) sentences.Add(sentence);
        }
        return Answer(status, sentences.ToArray(), unmapped);
    }

    // The codes of a refusal (S3b FIX1 rule: an item shaped like a code; what follows a code and is not one is its
    // detail, not a code; an item with no code before it is taken as a code). Null when the answer is not a code list.
    private static async Task<IReadOnlyList<string>?> CodesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not { } mediaType
            || !(mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                 || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
            return null;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !(document.RootElement.TryGetProperty("errors", out var errors)
                     || document.RootElement.TryGetProperty("Errors", out errors))
                || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() is 0 or > 20
                || errors.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                return null;
            var codes = new List<string>();
            foreach (var item in errors.EnumerateArray().Select(item => item.GetString()!))
                if (IsCode(item) || codes.Count == 0) codes.Add(item);
            return codes;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsCode(string value) => value.Length is > 2 and <= 100
        && value[0] is >= 'A' and <= 'Z'
        && value.Contains('_')
        && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    private static ObjectResult Answer(int status, string[] errors, bool unmapped) => new(unmapped
        ? new { success = false, errors, unmapped = true }
        : new { success = false, errors })
    {
        StatusCode = status
    };
}
