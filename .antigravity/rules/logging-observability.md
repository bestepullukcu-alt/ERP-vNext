---
description: "OBS-001 — Diten ERP vNext Yapılandırılmış Loglama, Hata Yönetimi ve İzlenebilirlik Standartları"
---

# Logging & Observability (Diten ERP vNext)

Bu doküman, sistemdeki tüm mikroservislerin (MDM, Auth, Gateway) nasıl log üreteceğini ve sistemin çalışma anındaki (Runtime) durumunun nasıl izleneceğini belirler.

## 📊 Yapılandırılmış Loglama (Structured Logging)

Sıradan metin logları yerine, makineler tarafından kolayca filtrelenebilen **Key/Value (Anahtar/Değer)** tabanlı loglama zorunludur.

- **Kütüphane:** Serilog (veya .NET 8 ILogger entegrasyonu).
- **Tenant İzleme:** Her log satırına mutlaka `TenantId` (GUID) bir meta-veri alanı olarak eklenmelidir. Bu sayede "X kiracısı neden yavaş?" sorusuna anında yanıt verilebilir.
- **Güvenlik (PII):** Log içeriklerinde asla şifre, kredi kartı veya kişisel veriler (TC No, Ad-Soyad gibi) açık metin olarak yer almamalıdır.
- **Payload Kuralı:** Hacim ve güvenlik nedeniyle Request Body varsayılan olarak loglanmaz; sadece kritik hata anlarında veya özel `Debug` modunda kontrollü loglanabilir.

[Image of a structured log entry example in JSON format showing timestamp, level, message template, and properties like TenantId and TraceId]

---

## 🛡️ Hata Yönetimi (Error Handling)
---
description: "OBS-001 — Diten ERP vNext Yapılandırılmış Loglama, Hata Yönetimi ve İzlenebilirlik Standartları"
---

# Logging & Observability (Diten ERP vNext)

Bu doküman, sistemdeki tüm mikroservislerin (MDM, Auth, Gateway) nasıl log üreteceğini ve sistemin çalışma anındaki durumunun nasıl izleneceğini belirler.

## 📊 Yapılandırılmış Loglama (Structured Logging)

Sıradan metin logları yerine, makineler tarafından kolayca filtrelenebilen Key/Value (Anahtar/Değer) tabanlı loglama zorunludur.

- Kütüphane: Serilog (veya .NET 8 ILogger entegrasyonu).
- Tenant İzleme: Her log satırına mutlaka TenantId (GUID) bir meta-veri alanı olarak eklenmelidir.
- Güvenlik (PII): Log içeriklerinde asla şifre, kredi kartı veya kişisel veriler (TC No, Ad-Soyad) açık metin olarak yer almamalıdır.
- Payload Kuralı: Hacim ve güvenlik nedeniyle Request Body varsayılan olarak loglanmaz.

---

## 🛡️ Hata Yönetimi (Error Handling)

Hatalar sistemin her yerinde aynı dilde konuşmalıdır:

- Global Exception Handling: Her mikroservis, yakalanamayan hataları merkezi bir middleware üzerinden yakalamalı ve ProblemDetails (RFC 7807) formatında dönmelidir.
- User Friendly Messages: Hata yanıtları içindeki mesajlar, frontend tarafındaki shared-resource.js ile uyumlu L10n key'leri içermelidir.
- Logging Level:
  - Information: Kritik iş akışları (Örn: "New Legal Entity Created").
  - Warning: Beklenen ama dikkat edilmesi gereken durumlar.
  - Error: İşlem iptaline neden olan teknik hatalar.

---

## 🔗 İzlenebilirlik (Observability) & Dağıtık Takip

Mikroservisler arası bir isteğin takibi için şu mekanizmalar kullanılır:

- Correlation / Trace ID: Gateway'den giren her isteğe benzersiz bir X-Correlation-Id atanır. Bu ID, tüm mikroservis geçişlerinde Header üzerinden taşınmalı ve her log satırına yazılmalıdır.
- Health Checks: Her servis /health endpoint'ine sahip olmalı; veritabanı ve bağımlı servislerin durumunu raporlamalıdır.
- Performance Tracing: 500ms'den uzun süren Handler işlemleri otomatik olarak Warning seviyesinde loglanmalı ve performans darboğazı olarak işaretlenmelidir.

---

## 🏗️ Uygulama Pratiği

LogContext kullanımı ile TenantId ve CorrelationId her zaman log mesajına enjekte edilmelidir. Veri sızıntısını önlemek için loglarda sadece GUID referansları kullanılmalı, hassas kullanıcı bilgileri (PII) temizlenmelidir.

---

## ✅ Kontrol Listesi
- [ ] Loglar JSON/Structured formatta mı?
- [ ] TenantId her log satırına meta-veri olarak ekleniyor mu?
- [ ] Hata anında ProblemDetails dönülüyor mu?
- [ ] CorrelationId servisler arası taşınıyor mu?
- [ ] Hassas veriler (PII) loglardan temizlendi mi?

---
Diten ERP vNext Observability Standard - OBS-001
Hatalar sistemin her yerinde aynı dilde konuşmalıdır:

- **Global Exception Handling:** Her mikroservis, yakalanamayan hataları merkezi bir middleware üzerinden yakalamalı ve `ProblemDetails` (RFC 7807) formatında dönmelidir.
- **User Friendly Messages:** Hata yanıtları içindeki mesajlar, frontend tarafındaki `shared-resource.js` ile uyumlu L10n key'leri içermelidir.
- **Logging Level:** - `Information`: Kritik iş akışları (Örn: "New Legal Entity Created").
  - `Warning`: Beklenen ama dikkat edilmesi gereken durumlar (Örn: "Invalid Login Attempt").
  - `Error`: İşlem iptaline neden olan teknik hatalar.

---

## 🔗 İzlenebilirlik (Observability) & Dağıtık Takip

Mikroservisler arası bir isteğin takibi için şu mekanizmalar kullanılır:

- **Correlation / Trace ID:** Gateway'den giren her isteğe benzersiz bir `X-Correlation-Id` atanır. Bu ID, tüm mikroservis geçişlerinde Header üzerinden taşınmalı ve her log satırına yazılmalıdır.
- **Health Checks:** Her servis `/health` endpoint'ine sahip olmalı; veritabanı ve bağımlı servislerin durumunu raporlamalıdır.
- **Performance Tracing:** 500ms'den uzun süren Handler işlemleri otomatik olarak `Warning` seviyesinde loglanmalı ve performans darboğazı olarak işaretlenmelidir.

---

## 🧩 Uygulama Örneği (Serilog Context)

```csharp
// Doğru Loglama Pratiği
using (LogContext.PushProperty("TenantId", _tenantContext.TenantId))
using (LogContext.PushProperty("CorrelationId", correlationId))
{