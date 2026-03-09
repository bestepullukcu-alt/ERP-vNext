---
description: "ERP-ARCH-001 — Diten ERP vNext Mikroservis Katmanlama, Bağımlılık ve CQRS Standartları"
---

# ERP Mimari Kuralları (Diten ERP vNext)

Bu doküman, sistemdeki her bir mikroservisin (MDM, Auth vb.) sahip olması gereken katmanlı mimari yapısını ve bu katmanlar arasındaki etkileşim kurallarını belirler.

## 🏗️ Katmanlı Mimari Yapısı (Layering)

Her mikroservis aşağıdaki 5 temel projeden oluşmalıdır:

1. **`<Service>.Api` (Presentation):** Dış dünyaya açılan kapı. Controller'lar, Middleware'ler ve Swagger burada yer alır.
2. **`<Service>.Application` (Orchestration):** İş mantığının (Business Logic) kalbi. CQRS Command/Query'ler, Handler'lar, Mapping ve Validation buradadır.
3. **`<Service>.Domain` (Core):** Sistemin en iç katmanı. Entity'ler, Value Object'ler, Domain Exception'lar ve Repository Interface'leri buradadır.
4. **`<Service>.Persistence` (Data Access):** MongoDB implementasyonu. DbContext, Repository sınıfları ve Tenant-Filter mantığı burada hapsedilir.
5. **`<Service>.Infrastructure` (Cross-Cutting):** Mail, SMS, File Storage gibi dış servis entegrasyonlarını barındırır.



---

## 🛡️ Bağımlılık Kuralları (Zorunlu)

- **Akış Yönü:** `Api -> Application -> Domain`.
- **Domain Bağımsızlığı:** Domain katmanı en merkezdedir; diğer hiçbir katmanı (Application, Api, Persistence) referans alamaz.
- **Ters Bağımlılık YASAK:** Üst katmanlar alt katmanları bilir, ancak alt katmanlar üsttekilerden habersizdir.
- **Soyutlama:** `Application` katmanı veritabanına doğrudan erişmez; `Domain` içinde tanımlanmış Interface'leri (`IRepository`) kullanır.

---

## ⚡ CQRS & MediatR Disiplini

- **Sıfır İş Mantığı (Controller):** Controller içinde `if-else` veya business logic bulunamaz. Sadece isteği alır ve MediatR üzerinden ilgili Command/Query'ye paslar.
- **Handler Bağımsızlığı:** Her Handler sadece kendi işinden sorumludur.
- **Validation Pipeline:** FluentValidation kullanılarak oluşturulan validasyonlar, Handler çalışmadan önce MediatR Pipeline üzerinden otomatik tetiklenmelidir.

---

## 🍃 Persistence (MongoDB) Standartları

- **Driver İzolasyonu:** `MongoDB.Driver` kütüphanesi sadece `Persistence` projesinde referanslanmalıdır. Diğer katmanlar sürücüye bağımlı olmamalıdır.
- **Otomatik Tenant Filtresi:** Her sorgu, anayasada belirtilen `X-Tenant-Id` (GUID) değerini otomatik olarak veritabanı seviyesinde filtrelemelidir.
- **Tracking:** Okuma işlemlerinde performans için `AsNoTracking` benzeri yaklaşımlar (Mongo için projeksiyonlar) tercih edilmelidir.

---

## 🚨 Genel Uygulama Kuralları

1. **Asenkron Yapı:** Tüm Girdi/Çıktı (I/O) işlemleri `async` olmalı ve `CancellationToken` mutlaka en alt katmana kadar iletilmelidir.
2. **Hata Yönetimi:** Tüm hatalar (Business veya System) `ProblemDetails` formatında, merkezi bir `GlobalExceptionHandler` üzerinden dönülmelidir.
3. **DTO Kullanımı:** Katmanlar arası veri taşıma için her zaman DTO'lar (Data Transfer Objects) kullanılmalıdır; Entity'ler asla API yanıtı olarak dönülmemelidir.
4. **Interface Standartı:** Bağımlılıklar (DI) her zaman Interface'ler üzerinden yönetilmelidir.

---

## ✅ Kontrol Listesi
- [ ] Proje yapısı 5 katmana uygun mu?
- [ ] Domain katmanı hiçbir dış projeye referans veriyor mu? (Kontrol et: Veriyorsa düzelt).
- [ ] Controller içinde MediatR dışında bir mantık var mı?
- [ ] MongoDB implementasyonu sadece Persistence içinde mi?