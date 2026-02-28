# Mongo Index Kuralları

## Minimum zorunluluk
- Her collection’da TenantId ile başlayan bir index olmalı:
  - { TenantId: 1, <doğal_anahtar veya sık filtre>: 1 }

## Kılavuz
- Sık kullanılan filter/sort alanlarına index ekle.
- Sınırsız regex araması yapma.
