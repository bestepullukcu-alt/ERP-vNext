---
description: Diten ERP vNext projelerinde Multi-Tenant (Çoklu Kiracı) veri izolasyonu ve yönetimi kuralları.
---

# Multi-Tenancy Kuralları (Single DB, Multi-Tenant)

Diten ERP vNext, tüm müşterilerin aynı veritabanını (MongoDB) paylaştığı ancak verilerin satır/doküman bazında izole edildiği bir mimariye sahiptir.

## 🔴 DEĞİŞMEZ KURALLAR (CRITICAL)

1. **GUID Zorunluluğu:** `TenantId` değerleri KESİNLİKLE `Guid` tipinde olmalıdır. Eski sistemlerdeki gibi `"1"`, `"0"` veya hardcoded string değerler KESİNLİKLE YASAKTIR.
2. **Entity Standardı:** MongoDB'ye kaydedilecek olan, tenant'a özel her Entity sınıfı `ITenantEntity` (veya benzeri bir base interface) uygulamalı ve içinde zorunlu `Guid TenantId { get; set; }` barındırmalıdır.
3. **X-Tenant-Id Header:** İstemciden (Frontend/Postman) gelen her API isteğinde `X-Tenant-Id` header'ı bulunmak zorundadır.
4. **DTO ve Body Yasağı:** Create/Update DTO'ları içinde veya JSON request body'sinde `TenantId` ASLA gönderilmez ve istenmez.
5. **Server-Side Çözümleme:** TenantId, API tarafında bir Middleware veya `TenantContext` servisi tarafından `HttpContext.Request.Headers` içinden okunur ve doğrudan Repository katmanına (veya Command Handler'a) enjekte edilir.
6. **Sızıntı Koruması (Leak Prevention):** Repository base sınıflarında tüm `Find`, `Update`, `Delete` işlemleri otomatik olarak `CurrentTenantId` filtresi ile sarmalanmalıdır. Geliştiricinin bunu manuel yazmasına güvenilmez.