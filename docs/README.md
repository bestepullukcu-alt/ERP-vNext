# `docs/`

Bu, `docs/` kökündeki **tek** dosyadır. Kural:
[`.antigravity/rules/docs-organization.md`](../.antigravity/rules/docs-organization.md)

## Belgemi nereye koyayım?

Klasörü **belgenin zamanla nasıl davrandığı** belirler — konusu değil.
Sırayla sor, ilk "evet"te dur:

| # | soru | klasör |
| :-- | :--- | :--- |
| 1 | Biz mi yazdık? **Hayır**, dışarıdan geldi | `vendor/` |
| 2 | Belirli bir tarihte olanı mı kaydediyor, bir daha değişmeyecek mi? | `records/` |
| 3 | Henüz olmamış bir şeyi mi anlatıyor? | `roadmap/` |
| 4 | Birine bir işi nasıl yapacağını mı öğretiyor? | `guides/` |
| 5 | Bugünün gerçeğini mi anlatıyor, değişince güncellenecek mi? | `reference/` |

Beş sorunun dışında kalan belge yoktur. **Köke dosya konmaz** ve altıncı bir üst
klasör açmak Control Tower kararıdır.

## Ne nerede

| klasör | ne var | değişir mi |
| :--- | :--- | :--- |
| `reference/` | mimari · modül belgeleri (`platform/` ve `tenant/`) · Blueprint · entegrasyon sözleşmeleri · referans veri | **evet** — gerçek değişince güncellenir |
| `guides/` | kullanım kılavuzları · dev ortam · Control Tower SOP · üst kaynak SOP | evet |
| `records/` | denetimler (`audits/<yyyy-mm>/`) · analizler · kabul raporları · sürüm notları | **hayır** — yazıldığı gibi kalır |
| `roadmap/` | planlar · `backlog/product-backlog.md` | evet |
| `vendor/` | dışarıdan gelen teslimatlar, geldiği adla | **hayır** — düzenlenmez |

## Sık aranan

- Açık maddeler → `roadmap/backlog/product-backlog.md`
- MOD-xxxx kimliğinin kanonik kaynağı → `reference/blueprint/`
- Dev ortam kurulumu → `guides/operations/dev-environment.md`
- Bir modülün belgeleri → `reference/modules/platform/…` veya `reference/modules/tenant/…`
- Bir denetim raporu → `records/audits/<yyyy-mm>/`

## Belge taşırken

Taşıma, ona işaret eden her bağı kırar — ve en tehlikelileri belgelerde değil,
**kodda** olanlardır. Kural §4'teki protokolü uygula: ölç, `git mv`, referansları
aynı commit'te güncelle, kod uzantılarını da tara, sonra ölü bağ kalmadığını
**göster**.
