# Workflow: Mongo Collection Ekle

## Gerekli input
- Entity adı ve alanları
- Doğal anahtar / unique ihtiyacı
- Beklenen sorgular (filter/sort)

## Kurallar
- Document ITenantDocument uygular (TenantId zorunlu)
- Index ekle: TenantId + sık filtre
- Repository methodlar (tenant enforced)
- Önce plan, sonra implement
