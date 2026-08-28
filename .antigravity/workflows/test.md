---
description: [Test Oluşturma ve Çalıştırma Komutu — Diten ERP vNext (.NET 8)]
---
# /test - Test Oluşturma ve Yürütme

Bu komut; yeni testler oluşturur, mevcut testleri çalıştırır veya test kapsamını (coverage) kontrol eder.

---

## 🏗️ Alt Komutlar

- `/test`                - Tüm projeyi test et (dotnet test)
- `/test [dosya/özellik]` - Belirli bir hedef için Unit/Integration testleri üret
- `/test coverage`       - Test kapsama raporunu göster
- `/test tenant-safety`  - Sadece Tenant izolasyon testlerini çalıştır

---

## 🛡️ Diten Test Standartları (Kurallar)

1. **AAA Deseni:** Testler mutlaka "Arrange (Hazırla) - Act (Çalıştır) - Assert (Doğrula)" yapısında olmalıdır.
2. **Mocking:** Veritabanı (MongoDB) ve dış servisler mutlaka `Moq` veya `NSubstitute` ile taklit edilmelidir.
3. **Multi-Tenancy Check:** Her test senaryosu mutlaka "Farklı TenantId" durumunu test etmelidir.
4. **Dil Check:** Hata mesajlarının `SharedLocalizer` üzerinden modül türüne göre doğru Key ile dönüp dönmediği kontrol edilmelidir.

---

## 📝 Çıktı Formatı (Örnek)

### Test Planı
| Senaryo | Tür | Kapsam |
|-----------|------|----------|
| Şehir başarıyla oluşturulmalı | Unit | Happy Path |
| Geçersiz TenantId reddedilmeli | Security | İzolasyon |
| Boş isim hatası dönmeli | Validation | L10n |

### Üretilen Test (C# / xUnit)
```csharp
[Fact]
public async Task CreateCity_WithDifferentTenant_ShouldThrowSecurityException()
{
    // Arrange: Farklı bir TenantId ile istek hazırla
    // Act: Handler'ı çağır
    // Assert: UnauthorizedAccessException fırlatıldığını doğrula
}
---

## 🍃 Gerçek Mongo'ya Bağlanan Testler

Yukarıdaki taklit (mock) kuralı birim testler içindir. **Gerçek Mongo'ya bağlanan** bir test yazıyorsan
([DB-010](../rules/mongo-indexing.md#-test-veritabanları-db-010)):

- Koşu başına **yeni veritabanı yaratma** — izolasyon `TenantId` ile sağlanır.
- `MongoDbIndexConfigurations.EnsureIndexesAsync` **çağırma** — o üretim açılış yoludur, tüm şemayı kurar.
  Yalnız ihtiyacın olan profili iste: `PlatformSchemaManifest.ApplyAsync(db, new[]{ SchemaProfile.X })`.

⚠ İhlal, testi kırmızıya döndürmez — **`mongod`'u öldürür** ve hata `Connection refused` diye okunur.
Muhafız: `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests`
