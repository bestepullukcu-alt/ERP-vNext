using System.Security.Cryptography;
using System.Text;
using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Persistence.Suppliers;

/// <summary>Göçün bir kiracı için ürettiği kayıtlar ve sayıları. Ad, vergi no ya da kiracı adı taşımaz — yalnız sayı.</summary>
public sealed class SupplierMigrationPlan
{
    public List<SupplierMaster> Masters { get; } = new();
    public List<SupplierCompanyRelationship> Relationships { get; } = new();

    public int LegacyRecords { get; set; }
    public int LegacyDeleted { get; set; }
    public int SkippedWithoutTenantOrCompany { get; set; }
    public int Groups { get; set; }
    public int MergedRecords { get; set; }
    public int WithoutTaxId { get; set; }
    public int CodeConflicts { get; set; }
    public int SameCompanyDuplicates { get; set; }
    public int NameDifferences { get; set; }
    public int CountryNormalized { get; set; }
    public int ScreeningDifferences { get; set; }
}

/// <summary>
/// Birleştirme kuralı (MOD-0140 §8 "Göç"; öneri <c>docs/roadmap/plans/mvp2-supplier-tenant-01/PACK-CHANGE-PROPOSAL.md</c>
/// §2.1). Saf hesap: Mongo'ya dokunmaz, aynı girdiden aynı kayıtları üretir (kimlikler ve yeni kodlar eski kayıtların
/// kimliklerinden türetilir) — <c>dry-run</c> ile <c>apply</c> aynı planı görür, ikinci <c>apply</c> yeni bir şey bulmaz.
/// <list type="number">
/// <item>Grup anahtarı (TenantId, Country, TaxIdNormalized); ülke kırpılıp küçük harfe çevrilir (pack §4.1 "lowercase
/// code"). Vergi no'su boş kayıtlar birleşmez. Silinmiş eski kayıtlar gruplara girmez: her biri silinmiş bir kayıt + silinmiş
/// bir ilişki olur (eski belgelerdeki kodları çözülmeye devam eder).</item>
/// <item>Hayatta kalan: en eski CreatedAt, eşitlikte en küçük Id. Kiracı kaydının Id'si ve kodu onunkidir.</item>
/// <item>Ad ve ülke hayatta kalanınki (farklılık sayılır, veri değiştirilmez); iletişimler (Type, Email), belgeler
/// (Type, EvidenceRef) birleşimi; tarama en yeni değerlendirme (ScreenedAt = o kaydın UpdatedAt'ı, yoksa CreatedAt).</item>
/// <item>Her eski kayıttan bir ilişki (Id = eski kaydın Id'si). Eski kod kiracı kaydının kodundan farklıysa
/// LegacySupplierCodes'a girer. Normalizasyon aynı şirkette iki kaydı birleştirirse ikincisi ayrı ilişki açmaz; kodu ilk
/// ilişkinin eski kodlarına eklenir.</item>
/// <item>Kod çakışması: iki farklı firmanın aynı koda sahip olması → sonra gelen firmaya yeni kod; eski kodu kendi
/// ilişkisinde kalır.</item>
/// <item>Hayatta kalmayanlar kiracı kaydı olarak MergedIntoId + soft delete alır. Eski <c>suppliers</c> koleksiyonu
/// değişmez.</item>
/// </list>
/// </summary>
public static class SupplierMigrationPlanner
{
    public static SupplierMigrationPlan Plan(IEnumerable<Supplier> legacy, DateTimeOffset now)
    {
        var plan = new SupplierMigrationPlan();
        var all = legacy.ToList();
        plan.LegacyRecords = all.Count;

        foreach (var tenant in all.Where(Placeable).GroupBy(s => s.TenantId!.Value).OrderBy(g => g.Key))
        {
            PlanTenant(tenant.ToList(), now, plan);
        }

        plan.SkippedWithoutTenantOrCompany = all.Count(s => !Placeable(s));
        return plan;
    }

    private static bool Placeable(Supplier s) =>
        s.TenantId is { } t && t != Guid.Empty && s.LegalEntityId is { } le && le != Guid.Empty;

    private static void PlanTenant(List<Supplier> records, DateTimeOffset now, SupplierMigrationPlan plan)
    {
        var reservedCodes = new HashSet<string>(records.Select(r => r.SupplierId), StringComparer.Ordinal);
        var claimedCodes = new HashSet<string>(StringComparer.Ordinal);

        var active = records.Where(r => !r.IsDeleted).ToList();
        plan.WithoutTaxId += active.Count(r => SupplierTaxId.Normalize(r.TaxId) is null);
        plan.CountryNormalized += active.Count(r => r.Country is not null && r.Country != NormalizeCountry(r.Country));

        var groups = active
            .GroupBy(GroupKey)
            .Select(g => g.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList())
            .OrderBy(g => g[0].CreatedAt).ThenBy(g => g[0].Id)
            .ToList();

        foreach (var members in groups)
        {
            var survivor = members[0];
            plan.Groups++;
            plan.MergedRecords += members.Count - 1;

            var code = survivor.SupplierId;
            if (!claimedCodes.Add(code))
            {
                plan.CodeConflicts++;
                code = NewCode(survivor.Id, reservedCodes);
                reservedCodes.Add(code);
                claimedCodes.Add(code);
            }

            if (members.Any(m => !string.Equals(m.Name.Trim(), survivor.Name.Trim(), StringComparison.Ordinal)))
            {
                plan.NameDifferences++;
            }

            if (members.Select(m => (m.KycOutcome, m.SanctionsOutcome)).Distinct().Count() > 1)
            {
                plan.ScreeningDifferences++;
            }

            var latest = members.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id).First();
            plan.Masters.Add(new SupplierMaster
            {
                Id = survivor.Id,
                TenantId = survivor.TenantId,
                LegalEntityId = null,
                SupplierId = code,
                Name = survivor.Name,
                Country = NormalizeCountry(survivor.Country),
                TaxId = survivor.TaxId,
                TaxIdNormalized = SupplierTaxId.Normalize(survivor.TaxId),
                Contacts = members.SelectMany(m => m.Contacts).DistinctBy(ContactKey).Select(Copy).ToList(),
                Documents = members.SelectMany(m => m.Documents)
                    .DistinctBy(d => (d.Type, d.EvidenceRef))
                    .Select(Copy).ToList(),
                Screening = new SupplierScreening
                {
                    KycOutcome = latest.KycOutcome,
                    SanctionsOutcome = latest.SanctionsOutcome,
                    ScreenedAt = latest.UpdatedAt ?? latest.CreatedAt
                },
                CreatedAt = survivor.CreatedAt,
                UpdatedAt = survivor.UpdatedAt
            });

            foreach (var merged in members.Skip(1))
            {
                plan.Masters.Add(MasterOf(merged, now, mergedInto: survivor.Id));
            }

            var byCompany = new Dictionary<Guid, SupplierCompanyRelationship>();
            foreach (var member in members)
            {
                var company = member.LegalEntityId!.Value;
                if (byCompany.TryGetValue(company, out var existing))
                {
                    plan.SameCompanyDuplicates++;
                    AddLegacyCode(existing, member.SupplierId, code);
                    continue;
                }

                var relationship = RelationshipOf(member, supplierRef: survivor.Id);
                AddLegacyCode(relationship, member.SupplierId, code);
                byCompany[company] = relationship;
                plan.Relationships.Add(relationship);
            }
        }

        foreach (var deleted in records.Where(r => r.IsDeleted).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id))
        {
            plan.LegacyDeleted++;
            var deletedAt = deleted.DeletedAt ?? now;
            plan.Masters.Add(MasterOf(deleted, deletedAt, mergedInto: null));
            var relationship = RelationshipOf(deleted, supplierRef: deleted.Id);
            relationship.IsDeleted = true;
            relationship.DeletedAt = deletedAt;
            plan.Relationships.Add(relationship);
        }
    }

    private static (string? Country, string Key) GroupKey(Supplier s)
    {
        var tax = SupplierTaxId.Normalize(s.TaxId);
        // Vergi no'suz kayıt kendi grubudur: anahtar kaydın kimliği (birleşmez).
        return (NormalizeCountry(s.Country), tax is null ? "id:" + s.Id.ToString("N") : "tax:" + tax);
    }

    public static string? NormalizeCountry(string? country)
    {
        var trimmed = country?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    private static SupplierMaster MasterOf(Supplier s, DateTimeOffset deletedAt, Guid? mergedInto) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        LegalEntityId = null,
        SupplierId = s.SupplierId,
        Name = s.Name,
        Country = NormalizeCountry(s.Country),
        TaxId = s.TaxId,
        TaxIdNormalized = SupplierTaxId.Normalize(s.TaxId),
        Contacts = s.Contacts.Select(Copy).ToList(),
        Documents = s.Documents.Select(Copy).ToList(),
        Screening = new SupplierScreening
        {
            KycOutcome = s.KycOutcome,
            SanctionsOutcome = s.SanctionsOutcome,
            ScreenedAt = s.UpdatedAt ?? s.CreatedAt
        },
        MergedIntoId = mergedInto,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        IsDeleted = true,
        DeletedAt = s.IsDeleted ? s.DeletedAt ?? deletedAt : deletedAt
    };

    private static SupplierCompanyRelationship RelationshipOf(Supplier s, Guid supplierRef) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        LegalEntityId = s.LegalEntityId,
        SupplierRef = supplierRef,
        Status = s.Status,
        OnboardingStatus = s.OnboardingStatus,
        Approval = s.Approval is null
            ? null
            : new SupplierRelationshipApproval
            {
                WorkflowInstanceId = s.Approval.WorkflowInstanceId,
                DecidedBy = s.Approval.DecidedBy,
                DecidedAt = s.Approval.DecidedAt,
                SubmissionRound = string.IsNullOrEmpty(s.Approval.WorkflowInstanceId) ? null : 1
            },
        OnboardingCase = s.OnboardingCase is null
            ? null
            : new OnboardingCase
            {
                OnboardingStatus = s.OnboardingCase.OnboardingStatus,
                KycOutcome = s.OnboardingCase.KycOutcome,
                SanctionsOutcome = s.OnboardingCase.SanctionsOutcome,
                Documents = s.OnboardingCase.Documents.Select(Copy).ToList()
            },
        SourceSystem = s.SourceSystem,
        ExternalRef = s.ExternalRef,
        IdempotencyKey = s.IdempotencyKey,
        OnboardingIdempotencyKey = s.OnboardingIdempotencyKey,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private static void AddLegacyCode(SupplierCompanyRelationship relationship, string legacyCode, string masterCode)
    {
        if (!string.Equals(legacyCode, masterCode, StringComparison.Ordinal) && !relationship.LegacySupplierCodes.Contains(legacyCode))
        {
            relationship.LegacySupplierCodes.Add(legacyCode);
        }
    }

    /// <summary>Çakışan firmaya yeni kod: biçim aynı (<c>SUP-</c> + 8 büyük onaltılık), eski kaydın Id'sinden türetilir.</summary>
    private static string NewCode(Guid legacyId, HashSet<string> reserved)
    {
        for (var attempt = 0; ; attempt++)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{legacyId:N}:{attempt}"));
            var code = "SUP-" + Convert.ToHexString(hash)[..8];
            if (!reserved.Contains(code))
            {
                return code;
            }
        }
    }

    /// <summary>(Type, Email) — e-postası olmayan satır, telefonu ve adıyla ayrılır; yoksa e-postasız her satır tek satıra düşerdi.</summary>
    private static (string Type, string Email, string? Phone, string? Name) ContactKey(SupplierContact c)
    {
        var email = (c.Email ?? string.Empty).Trim().ToUpperInvariant();
        return email.Length > 0 ? (c.Type, email, null, null) : (c.Type, email, c.Phone?.Trim(), c.Name?.Trim());
    }

    private static SupplierContact Copy(SupplierContact c) => new() { Type = c.Type, Email = c.Email, Phone = c.Phone, Name = c.Name };

    private static SupplierDocument Copy(SupplierDocument d) => new() { Type = d.Type, EvidenceRef = d.EvidenceRef };
}
