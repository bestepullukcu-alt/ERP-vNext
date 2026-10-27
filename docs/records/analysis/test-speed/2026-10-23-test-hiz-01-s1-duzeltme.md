# TEST-HIZ-01 · S1 sabotajı: düzeltme kaydı (2026-10-23)

> **Tür:** tarihli kayıt. `2026-10-23-test-hiz-01-sabotaj.md` §4'teki çıkarımı düzeltir; o kayıt değiştirilmedi (K4).

## Ne yanlıştı

§4, S1 sabotajında (`TenantCommercialSubscriptionsController.cs:14`, `[Authorize(Policy = "PlatformActor")]`) hiçbir
testin kırmızıya dönmemesini **test boşluğu** diye okudu. Gerekçesi şuydu: GET uçlarında tenant kullanıcısını yalnız bu
satır durduruyor. Bu çıkarım kod okumasından gelmişti ve ölçülmemişti.

## Ölçülen

Yeni test: `A_tenant_user_is_refused_on_every_read_endpoint_even_with_the_read_keys_in_the_token`. 5 GET ucunu deniyor;
tenant kullanıcısının token'ında `read` ve `history.read` anahtarları da var.

| # | Sabotaj | Koşulan | Kırmızıya dönen |
|---|---|---|---|
| — | yok | sınıf (51 test) | 0 — yeni test yeşil (403) |
| G1 | controller satırı (S1) | sınıf (51 test) | **0** — yeni test de yeşil |
| G2 | `TenantResolutionMiddleware.cs:123–126` · yönetici yolunda platform aktörü olmayana verilen 403 | Platform hızlı kümesi + sınıf (6.949 test) | **0** |
| G3 | G1 + G2 birlikte | sınıf (51 test) | **5** — yalnız yeni testin 5 satırı |

## Doğru okuma

- GET uçlarını iki katman koruyor: controller'ın `PlatformActor` politikası ve paylaşılan `TenantResolutionMiddleware`'in
  yönetici yolu kuralı. Biri kaldırılınca öteki tutuyor. Bu bir test boşluğu değil, iki katmanlı koruma.
- Platform testlerinde **iki katmanın hiçbiri tek başına korunmuyor** (G1 ve G2'de 0 kırmızı). İkisi birden
  kaldırılırsa bunu yalnız yeni test yakalıyor (G3).
- Ara katman `Diten.Platform.Common`'da ve başka servisler de kullanıyor. O servislerin testleri bu ölçümde koşulmadı.
