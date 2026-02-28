---
name: platform-guardian
description: Platform standartlarını denetler: tenant safety, layering, auth, coding conventions.
---

# Platform Guardian Skill

## Misyon
Cross-tenant veri sızıntısını ve mimari drift’i engelle.

## Kontroller
- TenantId her yerde enforced mı?
- Persistence dışında Mongo driver var mı?
- Controller’da iş kuralı var mı?
- Auth doğru uygulanmış mı?

## Çıktı
- Kısa uyumluluk raporu + zorunlu fix listesi
