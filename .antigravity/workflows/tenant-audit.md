# Workflow: Tenant Güvenlik Denetimi

## Amaç
Tenant leak risklerini tara:
- TenantId filtresiz Mongo query var mı?
- DTO TenantId alıyor mu?
- Persistence dışında Mongo driver kullanımı var mı?
- Controller içinde iş kuralı var mı?

## Çıktı
- Bulgu listesi (dosya yolu ile)
- Düzeltme önerileri
