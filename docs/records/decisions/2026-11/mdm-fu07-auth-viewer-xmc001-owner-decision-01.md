# MOD-0290-FU07 — Auth Viewer satırı için sahip izni (XMC-001)

tarih: 2026-11-10
karar veren: sahip, bu oturumda kendi tıklamasıyla (soru kartı)
kayıt eden: CT · Ürün ana verisi (mdm-ct-02)

## Soru

MOD-0290-FU07 (Ürün kimlik numaraları — GTIN) yeni okuma anahtarı `mdm.product-identifiers.read` getiriyor. Auth `DefaultRolePermissionTemplate.SelectFor` bugün `read` eylemli her kiracı anahtarını varsayılan Viewer'a veriyor; anahtar `EntitlementOnlyViewerPermissions` listesinde olmadıkça modül hakkı olmayan kiracının Viewer'ı da onu alır. Önlemek için Auth listesine tek satır gerekiyor; bu başka modülün dosyası (XMC-001).

## Seçenekler

1. İzin ver, ayrı küçük iş (önerilen)
2. İzin ver, ikinci adımın (DL-2) içinde
3. Şimdi değil

## Karar

"İzin ver, ayrı küçük iş (Önerilen)".

## Sonuç

- Auth satırı kendi testiyle ayrı küçük bir XMC-001 işi olarak yapılır; FU07 paketinin Repo Scope'una Auth dosyası girmez.
- FU07 DL-2 manifesti, bu satırı taşıyan Auth sürümü pilot ve canlı ortamda dağıtılıp doğrulanmadan kaydolmaz (inceleme bulgusu F-29).
- DL-1 (veri tarafı) bu işi beklemez.
- Bu izin yalnız bu tek satır içindir; kod, commit, push ya da dağıtım izni değildir.
