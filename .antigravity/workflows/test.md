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
- `/test pre-mac-ui [module]` - UI draft'ı Mac koşusundan önce statik Pre-Mac UI Checklist'e göre kontrol et (aşağıda)

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

---

## 🧪 Pre-Mac UI Checklist (`/test pre-mac-ui`)

Bir UI draft Mac build/runtime koşusuna gönderilmeden **önce** (LANE içinde, kod çalıştırmadan):

1. `.antigravity/agents/frontend-ui-ux.md` → **Pre-Mac UI Checklist** bölümündeki UI-PM-01…UI-PM-12 maddelerini draft'ın view ve JS dosyalarına statik olarak uygula (grep/okuma).
2. Her madde için bir satır yaz: `item · PASS / FAIL / N/A · file:line kanıt`. N/A yalnızca madde deseni kullanılmıyorsa (ör. `ajax` yok → UI-PM-03).
3. **FAIL varsa** draft önce LANE'de düzeltilir; Mac koşusu FAIL kalan bir checklist ile başlatılmaz.
4. Checklist Mac §32.11 kabul koşusunun yerini **tutmaz**: odak, Escape, skeleton ve RTL maddeleri Mac'te Playwright ile ayrıca doğrulanır (klavye satırı + `ar` koşusu dahil).
