using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Services;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.ESignature.Providers;

/// <summary>
/// MOD-0022 E2 — the READ-COMPATIBLE Document Management provider (prompt: "FU23 için okuma-uyumlu bir sağlayıcı, FU23
/// kaydını değiştirmeden"). It asks FU23's own <see cref="DocumentSignableSubjectResolver"/> — the same tenant-scoped
/// projection FU23 hashes — and reads the field list back out of FU23's canonical JSON, so the MOD-0022 subject hash is
/// over exactly the governance fields FU23 signs. Nothing in FU23 is changed, called for writing, or re-registered; FU23
/// keeps signing through its own 16 endpoints until E6 (CT-D3).
///
/// <para>Wire shape: <c>module = document-management</c>, <c>subjectType</c> = the FU23 subject type in kebab case (e.g.
/// <c>quality-event</c>), <c>subjectId</c> = the subject's Guid, or <c>{subjectId}:{registerEntryId}</c> for the two
/// types FU23 resolves through a register entry (approval / release-gate evidence).</para>
///
/// <para>KS-2 limit, measured and stated: Document Management applies no LegalEntity filter today (backlog BL-432), so
/// visibility is the tenant boundary FU23's repositories enforce and <c>legalEntityId</c> is null.</para>
/// </summary>
public sealed class DocumentManagementSignableSubjectProvider : ISignableSubjectProvider
{
    public const string ModuleCode = "document-management";

    private readonly DocumentSignableSubjectResolver _resolver;

    public DocumentManagementSignableSubjectProvider(DocumentSignableSubjectResolver resolver) => _resolver = resolver;

    public string Module => ModuleCode;

    public static string WireType(SignableSubjectType type)
    {
        var name = type.ToString();
        var builder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    public async Task<SignableSubjectResolution> ResolveAsync(string subjectType, string subjectId, CancellationToken ct)
    {
        var type = Enum.GetValues<SignableSubjectType>().Cast<SignableSubjectType?>()
            .FirstOrDefault(t => string.Equals(WireType(t!.Value), subjectType, StringComparison.Ordinal));
        if (type is null || !DocumentSignableSubjectResolver.IsResolvable(type.Value))
        {
            return SignableSubjectResolution.NotFound;
        }

        var parts = subjectId.Split(':');
        if (!Guid.TryParse(parts[0], out var id) || parts.Length > 2)
        {
            return SignableSubjectResolution.NotFound;
        }

        Guid? registerEntryId = null;
        if (DocumentSignableSubjectResolver.RequiresRegisterEntryId(type.Value))
        {
            if (parts.Length != 2 || !Guid.TryParse(parts[1], out var entry))
            {
                return SignableSubjectResolution.NotFound;
            }

            registerEntryId = entry;
        }
        else if (parts.Length != 1)
        {
            return SignableSubjectResolution.NotFound;
        }

        var snapshot = await _resolver.ResolveAsync(type.Value, id, registerEntryId, ct);
        if (snapshot is null)
        {
            return SignableSubjectResolution.NotFound;
        }

        var fields = ReadFields(snapshot.CanonicalProjection);
        if (fields is null)
        {
            return SignableSubjectResolution.Unavailable;
        }

        var summary = fields
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Take(20)
            .Select(p => new SignableSubjectLine(Truncate(p.Key, 60), Truncate(p.Value!, 300)))
            .ToList();
        return SignableSubjectResolution.Found(new SignableSubject(
            Truncate($"Document Management · {type.Value} · {id:D}", 300), summary, LegalEntityId: null, fields));
    }

    /// <summary>FU23 canonical form: <c>{"subjectType":…,"subjectId":…,"fields":{…string or null…}}</c>.</summary>
    internal static Dictionary<string, string?>? ReadFields(string canonical)
    {
        try
        {
            using var document = JsonDocument.Parse(canonical);
            if (!document.RootElement.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var property in fields.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => property.Value.GetString(),
                    _ => property.Value.GetRawText()
                };
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
