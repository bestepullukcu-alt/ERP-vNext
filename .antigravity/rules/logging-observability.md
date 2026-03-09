---
description: "OBS-001 — Diten ERP vNext Yapılandırılmış Loglama, Hata Yönetimi ve İzlenebilirlik Standartları"
---

# 📊 Logging & Observability (Diten ERP vNext)

Bu doküman, sistemdeki mikroservislerin (MDM, Auth, Gateway) log üretim standartlarını ve çalışma anı (Runtime) izlenebilirliğini belirler.

---

## 🔍 1. Yapılandırılmış Loglama (Structured Logging)

Sıradan metin logları yerine, makineler tarafından kolayca filtrelenebilen **Key/Value** tabanlı loglama zorunludur.

- **Kütüphane:** Serilog (.NET 8 ILogger entegrasyonu ile).
- **Tenant Context:** Her log satırına mutlaka `TenantId` (GUID) meta-veri olarak eklenmelidir.
- **Güvenlik (PII):** Şifre, kredi kartı veya kişisel veriler (TC No, Ad-Soyad) loglarda asla açık metin bulunamaz.
- **Payload Kuralı:** Hacim nedeniyle Request Body loglanmaz; sadece kritik hata anlarında kontrollü loglanabilir.

---

## 🛡️ 2. Hata Yönetimi (Error Handling)

Hatalar tüm katmanlarda aynı standart dilde konuşmalıdır:

- **RFC 7807 (ProblemDetails):** Yakalanamayan hatalar merkezi bir middleware üzerinden bu formatta dönülmelidir.
- **L10n Entegrasyonu:** Hata mesajları frontend tarafındaki `shared-resource.js` ile uyumlu dil anahtarları (key) içermelidir.
- **Logging Seviyeleri:**
  - `Information`: Kritik iş akışları (Örn: "New Legal Entity Created").
  - `Warning`: Beklenen ama dikkat edilmesi gereken durumlar (Örn: 500ms+ süren işlemler).
  - `Error`: İşlem iptaline neden olan teknik istisnalar (Exceptions).

---

## 🔗 3. İzlenebilirlik & Dağıtık Takip (Observability)

- **Correlation ID:** Gateway'den giren her isteğe benzersiz bir `X-Correlation-Id` atanır. Bu ID, servisler arası geçişte Header üzerinden taşınmalı ve her log satırına yazılmalıdır.
- **Health Checks:** Her servis `/health` endpoint'ine sahip olmalı; DB ve bağımlı servis durumlarını raporlamalıdır.
- **Performance Tracing:** 500ms'den uzun süren tüm Handler işlemleri otomatik olarak `Warning` seviyesinde işaretlenmelidir.

---

## 🧩 4. Uygulama Örneği (Serilog Context)

LogContext kullanımı ile `TenantId` ve `CorrelationId` her zaman log mesajına enjekte edilmelidir:

```csharp
using (LogContext.PushProperty("TenantId", _tenantContext.TenantId))
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    _logger.LogInformation("Processing entity {EntityId}", entityId);
}