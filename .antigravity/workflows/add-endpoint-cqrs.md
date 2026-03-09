---
description: "WORKFLOW-001 — Diten ERP vNext Yeni Endpoint ve CQRS Geliştirme Akışı"
---

# Workflow: Endpoint Ekle (CQRS)

Bu akış, sistemde yeni bir API ucu oluşturulurken izlenecek standart operasyon adımlarını tanımlar.

## 📥 1. Gerekli Inputlar (Planlama Aşaması)
Geliştirmeye başlamadan önce şu bilgiler netleşmelidir:
- **İşlem:** HTTP Method (GET, POST, etc.) + Route (Standard: `/api/v1/...`).
- **Veri Modeli:** Request/Response DTO şemaları.
- **Güvenlik:** Auth gereksinimi (Default: `[Authorize]`).
- **Kurallar:** Validation kuralları ve Mongo Entity/Collection eşleşmesi.

---

## 🏗️ 2. Mimari ve Klasör Yapısı (Zorunlu)

Diten ERP vNext'te **CQRS Klasör Hiyerarşisi** aşağıdaki gibi sabitlenmiştir:



- **`Commands/`**: Sadece veriyi taşıyan `IRequest` modelleri (Örn: `CreateCityCommand.cs`).
- **`Queries/`**: Sadece sorgu modelleri (Örn: `GetCityByIdQuery.cs`).
- **`Handlers/`**: Gerçek iş mantığının (Business Logic) döndüğü yer.
  - **`CommandHandlers/`**: Yazma işlemlerini yöneten sınıflar.
  - **`QueryHandlers/`**: Okuma işlemlerini yöneten sınıflar.
- **`Validators/`**: FluentValidation sınıfları.

---

## 🛡️ 3. Uygulama Kuralları (Mühürlü)

1. **Controller Disiplini:** Controller sadece MediatR çağırır. İçinde `if`, `foreach` veya veritabanı sorgusu asla bulunamaz.
2. **DTO Temizliği:** DTO’lar asla `TenantId` içermez. Bu bilgi `X-Tenant-Id` header'ından otomatik enjekte edilir.
3. **Validation:** Her Command/Query için bir validator eklenmelidir.
4. **Repository Kullanımı:** Veriye erişim sadece Repository üzerinden yapılır ve `TenantId` filtresi otomatik uygulanır.
5. **Multi-Language:** Kullanıcıya dönen mesajlar (Toast, Hata vb.) her zaman `SharedResource` üzerinden 8 dilde sunulur.

---

## 🚀 4. Uygulama Sıralaması (Execution Steps)

1. **Plan:** Önce yapılacakları listeleyerek benden (Orkestratör) onay al.
2. **Domain/Persistence:** Entity ve Repository katmanını hazırla.
3. **Application:** Command/Query, DTO ve Handler sınıflarını oluştur.
4. **Validation:** İş kurallarını (Validator) yaz.
5. **Presentation:** API Controller ve Ocelot Route tanımlarını yap.
6. **Frontend:** (Gerekirse) View, JS ve L10n Bridge bağlantılarını kur.

---

## ✅ Kontrol Listesi
- [ ] Handler'lar doğru `Handlers/` klasörü altında mı?
- [ ] DTO'da `TenantId` var mı? (Varsa sil!).
- [ ] `X-Tenant-Id` ve `Authorization` kontrolleri yapıldı mı?
- [ ] 8 dil desteği için key'ler eklendi mi?