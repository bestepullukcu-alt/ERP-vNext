using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.ProcurementService.Domain.Entities;

/// <summary>
/// Firmanın tarama sonucu (MOD-0140 §4.1 Screening, K-3): KYC / yaptırım sonucu firmanın olgusudur, kiracıda tek sonuç.
/// </summary>
public sealed class SupplierScreening
{
    public KycOutcome KycOutcome { get; set; } = KycOutcome.Pending;
    public SanctionsOutcome SanctionsOutcome { get; set; } = SanctionsOutcome.Pending;
    public DateTimeOffset? ScreenedAt { get; set; }
}

/// <summary>
/// Kiracıda tek tedarikçi kaydı (MOD-0140 §4.1, koleksiyon <c>supplier_masters</c>). Ortak alanlar (K-1) burada; şirkete
/// bağlı alanlar <see cref="SupplierCompanyRelationship"/>'tedir. <see cref="EntityBase.LegalEntityId"/> kullanılmaz (boş
/// kalır). Pack bu varlığı <c>Supplier</c> diye anar; bugünkü şirket bazlı <see cref="Supplier"/> (koleksiyon
/// <c>suppliers</c>) göç boyunca salt okunur kaynak olarak kaldığı için sınıf adı ayrıdır (DL-2).
/// </summary>
public sealed class SupplierMaster : EntityBase
{
    /// <summary>Public code (<c>SUP-xxxxxxxx</c>) — server-assigned; kiracıda unique (silinmemiş kayıtlarda).</summary>
    public string SupplierId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Lowercase ülke kodu, nullable.</summary>
    public string? Country { get; set; }

    public string? TaxId { get; set; }

    /// <summary>Server-computed (<see cref="SupplierTaxId.Normalize"/>); payload'da YOK. K-2 tekillik anahtarının parçası.</summary>
    public string? TaxIdNormalized { get; set; }

    public List<SupplierContact> Contacts { get; set; } = new();

    public List<SupplierDocument> Documents { get; set; } = new();

    public SupplierScreening Screening { get; set; } = new();

    /// <summary>Yalnız göç yazar: birleşen (hayatta kalmayan) kaydın hayatta kalan kayda işaretçisi; kayıt soft delete'tir.</summary>
    [BsonRepresentation(BsonType.String)]
    public Guid? MergedIntoId { get; set; }
}

/// <summary>Şirketin tarama sonucunu geçersiz kılması (MOD-0140 §4.2 ScreeningOverride, K-3). Reason zorunlu.</summary>
public sealed class SupplierScreeningOverride
{
    public SanctionsOutcome Outcome { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? By { get; set; }
    public DateTimeOffset? At { get; set; }
}

/// <summary>
/// İlişkinin onay bağlantısı (MOD-0140 §4.2 Approval). <see cref="SubmissionRound"/> = MOD-0023 idempotency anahtarındaki
/// <c>n</c> (teknik alan, sözleşmede yok; kuralı §8).
/// </summary>
public sealed class SupplierRelationshipApproval
{
    public string? WorkflowInstanceId { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public int? SubmissionRound { get; set; }
}

/// <summary>
/// Tedarikçinin bir şirketle ilişkisi (MOD-0140 §4.2, koleksiyon <c>supplier_company_relationships</c>). Şirket başına tek
/// kayıt (Tenant + LE + SupplierRef). Durum, onay, ödeme koşulu, tarama istisnası, dış eşleme ve eski kodlar şirketindir;
/// başka şirket bunları görmez.
/// </summary>
public sealed class SupplierCompanyRelationship : EntityBase
{
    /// <summary><see cref="SupplierMaster"/>.Id — aynı kiracıdaki kayıt.</summary>
    [BsonRepresentation(BsonType.String)]
    public Guid SupplierRef { get; set; }

    public SupplierStatus Status { get; set; } = SupplierStatus.Active;

    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.Draft;

    public SupplierRelationshipApproval? Approval { get; set; }

    public OnboardingCase? OnboardingCase { get; set; }

    public SupplierScreeningOverride? ScreeningOverride { get; set; }

    /// <summary>Ödeme koşulu kodu (K-8). Banka bilgisi YOK — Finance / Treasury'de.</summary>
    public string? PaymentTermsCode { get; set; }

    public bool PurchasingEnabled { get; set; }
    public DateTimeOffset? PurchasingSince { get; set; }

    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }

    /// <summary>Göçte bu şirketin eski kodları — takma ad (K-5). Geçmiş belgeler yeniden yazılmaz.</summary>
    public List<string> LegacySupplierCodes { get; set; } = new();

    /// <summary>Create idempotency anahtarı — komutun varlığında kalır (§4 notu), kapsamı bugünkü gibi Tenant + LE.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Onboarding gönderim idempotency anahtarı — ilişkide (onboarding şirket bazında).</summary>
    public string? OnboardingIdempotencyKey { get; set; }
}

/// <summary>Vergi no normalizasyonu (K-2): boşluk, nokta, tire atılır, büyük harfe çevrilir. Boş sonuç → null.</summary>
public static class SupplierTaxId
{
    public static string? Normalize(string? taxId)
    {
        if (taxId is null)
        {
            return null;
        }

        var chars = taxId.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray();
        return chars.Length == 0 ? null : new string(chars).ToUpperInvariant();
    }
}
