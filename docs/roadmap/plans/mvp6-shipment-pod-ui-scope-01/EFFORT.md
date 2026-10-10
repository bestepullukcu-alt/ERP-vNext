# Teslimat bazli efor tahmini

Bu tahmin kisi-gunudur; dosya veya test sayisindan turetilmemistir ve takvim taahhudu degildir. Her aralik, teslimatin
tamamlanmasi icin gereken karar, uygulama, integration ve bagimsiz kanit isini olcer.

| Teslimat | Iyimser | Olasi | Kotumser | Gerekce / belirsizlik |
|---|---:|---:|---:|---|
| Tasarim karari, pack delta uygulamasi ve UI Phase 1.5 kapanisi | 0.5 | 1.0 | 2.0 | Scope hazir; hedef checkout, manifest provider yolu veya preimage drift'i sureyi artirir. |
| UI-owned list/detail/create/transition/POD uygulamasi ve 7 dil | 5.0 | 8.0 | 13.0 | Repeatable line editor, Compact form/detail, lexical decimal, permission-specific actions ve erisilebilirlik ana is. |
| Shared gateway, module/permission registration ve navigation integration | 1.5 | 3.0 | 5.0 | Tek writer ve exact preimage gerektirir; mevcut SupplyChain manifest pattern'inin hedefte bulunmamasi risk. |
| Composed runtime ve bagimsiz browser VER | 2.5 | 4.5 | 8.0 | Auth/JWT ortami, Gateway route, DB izolasyonu, retry/unknown-result ve kalici browser artifact kabiliyeti belirleyici. |
| **Toplam** | **9.5** | **16.5** | **28.0** | Deliverable toplamidir; paralellik veya bekleme suresi eklenmemistir. |

## Kapsam belirsizlikleri

- Evidence references yalniz string referanstir. Upload UX veya document picker bu kapsamda yoktur.
- `itemId`, `skuId`, `uomId`, warehouse ve ship-to icin frozen lookup endpoint'i bu UI yuzeyinde yoktur; ilk dilim exact
  deger girisi/validation sunar. Lookup uydurulmaz.
- List contract server paging verir, fakat arama/siralama endpoint'i vermez. DataTables arama/siralama yalniz yuklenen
  sayfa icinde uygulanir ve UI bunu kullaniciya aciklar.
- Gateway ve module manifest preimageleri, final integration target secilmeden hash-bound hale gelemez.
- Kalici ekran goruntusu ve gercek Auth token zinciri ancak verifier ortaminda destek varsa kapanir; unit test ile ikame edilmez.

