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
4. **8 Dil Check:** Hata mesajlarının `SharedLocalizer` üzerinden doğru Key ile dönüp dönmediği kontrol edilmelidir.

---

## 📝 Çıktı Formatı (Örnek)

### Test Planı
| Senaryo | Tür | Kapsam |
|-----------|------|----------|
| Şehir başarıyla oluşturulmalı | Unit | Happy Path |
| Geçersiz TenantId reddedilmeli | Security | İzolasyon |
| Boş isim hatası (8 dil) dönmeli | Validation | L10n |

### Üretilen Test (C# / xUnit)
```csharp
[Fact]
public async Task CreateCity_WithDifferentTenant_ShouldThrowSecurityException()
{
    // Arrange: Farklı bir TenantId ile istek hazırla
    // Act: Handler'ı çağır
    // Assert: UnauthorizedAccessException fırlatıldığını doğrula
}