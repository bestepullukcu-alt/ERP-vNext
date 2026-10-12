using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Diten.Platform.Domain.Entities.ESignature;

/// <summary>
/// MOD-0022 — canonical JSON and SHA-256 digests (pack §8 "Canonical subject and digests", CT-D2; ESIGN-BUNDLE §7,
/// normative). The rules are FU23's (<c>DocumentSignableSubjectResolver.Canonicalize</c>) extended with <c>module</c>:
/// hand-built JSON, no whitespace, keys sorted by ordinal, values are JSON strings or the <c>null</c> literal, escapes
/// <c>\\ \" \n \r \t</c> only, dates UTC ISO-8601 "O", Guids "D", bools <c>true</c>/<c>false</c> (as strings).
///
/// <para>⚠ The output is a wire contract: the frozen <c>esign.openapi.yaml</c> carries golden vectors produced by this
/// class, and every stored <see cref="ESignatureRecord.SubjectHash"/> / <see cref="ESignatureRecord.SignatureDigest"/>
/// depends on it. Any change to ordering, escaping or formatting breaks verification of every existing signature —
/// the golden-vector tests are the guard.</para>
/// </summary>
public static class ESignatureDigest
{
    public const string SubjectHashAlgorithm = "CanonicalJsonSha256";

    /// <summary>
    /// <c>{"module":…,"subjectType":…,"subjectId":…,"fields":{…sorted…}}</c> — the top-level order is fixed (FU23 order
    /// with <c>module</c> first, ESIGN-BUNDLE §2.3); only the <c>fields</c> keys are sorted.
    /// </summary>
    public static string CanonicalSubject(string module, string subjectType, string subjectId, IReadOnlyDictionary<string, string?> fields)
    {
        var builder = new StringBuilder();
        builder.Append("{\"module\":").Append(Quote(module))
            .Append(",\"subjectType\":").Append(Quote(subjectType))
            .Append(",\"subjectId\":").Append(Quote(subjectId))
            .Append(",\"fields\":");
        AppendSortedObject(builder, fields);
        return builder.Append('}').ToString();
    }

    public static string SubjectHash(string module, string subjectType, string subjectId, IReadOnlyDictionary<string, string?> fields) =>
        Sha256Hex(CanonicalSubject(module, subjectType, subjectId, fields));

    /// <summary>
    /// The signature digest input (ESIGN-BUNDLE §7): every key sorted by ordinal, <c>context</c> nested and sorted too.
    /// </summary>
    public static string CanonicalSignature(SignatureDigestInput input)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["signatureNumber"] = input.SignatureNumber,
            ["tenantId"] = Guid(input.TenantId),
            ["module"] = input.Module,
            ["subjectType"] = input.SubjectType,
            ["subjectId"] = input.SubjectId,
            ["subjectHash"] = input.SubjectHash,
            ["legalEntityId"] = Guid(input.LegalEntityId),
            ["actionCode"] = input.ActionCode,
            ["context"] = input.Context,
            ["meaningCode"] = input.MeaningCode,
            ["meaningTextAtSign"] = input.MeaningTextAtSign,
            ["signerUserId"] = input.SignerUserId,
            ["signerNameAtSign"] = input.SignerNameAtSign,
            ["signedAtUtc"] = Iso(input.SignedAtUtc),
            ["reauthKind"] = input.ReauthKind,
            ["passwordVerifiedAtUtc"] = Iso(input.PasswordVerifiedAtUtc),
            ["appearanceType"] = input.AppearanceType,
            ["appearanceSampleId"] = Guid(input.AppearanceSampleId),
            ["appearanceImageHash"] = input.AppearanceImageHash,
            ["requestId"] = Guid(input.RequestId)
        };

        var builder = new StringBuilder("{");
        var first = true;
        foreach (var pair in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(Quote(pair.Key)).Append(':');
            switch (pair.Value)
            {
                case null:
                    builder.Append("null");
                    break;
                case IReadOnlyDictionary<string, string?> nested:
                    AppendSortedObject(builder, nested);
                    break;
                default:
                    builder.Append(Quote((string)pair.Value));
                    break;
            }
        }

        return builder.Append('}').ToString();
    }

    public static string SignatureDigest(SignatureDigestInput input) => Sha256Hex(CanonicalSignature(input));

    /// <summary><c>imageHash = hex(SHA-256(png bytes))</c>, lowercase.</summary>
    public static string ImageHash(ReadOnlySpan<byte> png) => Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();

    public static string Sha256Hex(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

    /// <summary>UTC ISO-8601 round-trip ("O"), the FU23 date form. Unspecified kinds are taken as UTC.</summary>
    public static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value, DateTimeKind.Utc)
            .ToString("O", CultureInfo.InvariantCulture);

    public static string Bool(bool value) => value ? "true" : "false";

    /// <summary>
    /// The precision a stored DateTime keeps (BSON dates are milliseconds). Every time that enters a digest is truncated to
    /// it first, so the digest recomputed from the stored record equals the one computed at signing.
    /// </summary>
    public static DateTime ToStoredPrecision(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    private static string? Iso(DateTime? value) => value is null ? null : Iso(value.Value);

    private static string? Guid(Guid? value) => value?.ToString("D");

    private static void AppendSortedObject(StringBuilder builder, IReadOnlyDictionary<string, string?>? values)
    {
        builder.Append('{');
        var first = true;
        foreach (var pair in (values ?? EmptyFields).OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append(Quote(pair.Key)).Append(':').Append(pair.Value is null ? "null" : Quote(pair.Value));
        }

        builder.Append('}');
    }

    private static readonly IReadOnlyDictionary<string, string?> EmptyFields = new Dictionary<string, string?>();

    private static string Quote(string value) => "\"" + Escape(value) + "\"";

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);
}

/// <summary>The twenty fields of the signature digest (ESIGN-BUNDLE §7). Enum-like values are passed as their wire names.</summary>
public sealed record SignatureDigestInput(
    string SignatureNumber,
    Guid TenantId,
    string Module,
    string SubjectType,
    string SubjectId,
    string SubjectHash,
    Guid? LegalEntityId,
    string? ActionCode,
    IReadOnlyDictionary<string, string?> Context,
    string MeaningCode,
    string MeaningTextAtSign,
    string SignerUserId,
    string SignerNameAtSign,
    DateTime SignedAtUtc,
    string ReauthKind,
    DateTime PasswordVerifiedAtUtc,
    string AppearanceType,
    Guid? AppearanceSampleId,
    string? AppearanceImageHash,
    Guid? RequestId);
