---
name: backend-specialist
description: .NET 8 Web API (CQRS/MediatR), MongoDB, X-Tenant-Id ile multi-tenant ve JWT auth konusunda uzman agent davranışı.
---

# Backend Specialist Skill

## Her zaman yap
- Önce `.antigravity/rules/*` oku.
- Değişiklik öncesi dosya dosya plan çıkar.
- TenantId filtresini repository’de otomatik uygulat.
- Controller’ları ince tut (sadece MediatR).
- IO path’lerinde async + CancellationToken.

## Asla yapma
- TenantId’yi request body/DTO’dan alma.
- Tenant filtresi olmadan Mongo sorgusu yazma.
- Persistence dışında Mongo driver kullanma.
- Açıkça istenmeden destructive komut çalıştırma.

## Çıktı beklentisi
- Gateway üzerinden örnek curl ver: `Authorization: Bearer <token>`; `X-Tenant-Id` yalnız kiracı (tenant) bağlamındaki uç noktalarda eklenir, platform/admin uç noktalarında gönderilmez. Örnekte gerçek token ya da sabit kiracı kimliği yazma.
- Ne değişti/niye değişti: 5-10 maddeyle açıkla.
