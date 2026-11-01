# PVG ilk dalga — entegrasyon kapısı raporu (WP-0230-DL2A, WP-0230-DL3A, WP-0230-DL8)

> Control Tower kaydı, 2026-10-23. Bu kayıt, kuyrukta `VERIFIED → INTEGRATION_READY → INTEGRATED` geçişinin kanıtıdır.
> ct-sop7-transitions iki koşul ister: entegrasyon gerektiğinin yazılı olması ve entegrasyon kapısı raporunun PASS vermesi.
> Bu kayıt kabul kaydı değildir. `ACCEPTED` için künyeli CT kabul kaydı (CT-OPS-03) gerekir; ölçüm bu kaydın sonundaki "Kabul engeli" bölümünde.
> Önceki adım: `docs/records/audits/2026-10/pvg-wave1-verify-01/VERIFY.md`.

## Entegrasyon gerekliydi

DL2A ve DL3A ürün kodu değiştirdi. Bu yüzden ikisinin kodu `main`'e PR ile girdi.

DL8'de kod değişikliği yok. DL8'in ölçtüğü `ocelot.json`, `95563dae1`'den bu yana `main`'de değişmedi. Bu kayıttaki ölçüm, DL8'in kabul ölçütünü `main` kodunda yeniden doğruluyor.

## Birleşmeler (GitHub)

| WP | PR | Birleşme | Onaylayan / birleştiren | CI |
|---|---|---|---|---|
| WP-0230-DL3A | [#213](https://github.com/bestepullukcu-alt/ERP-vNext/pull/213) | `50600c5b3`, 2026-10-23 11:49 UTC | bestepullukcu-alt (APPROVED) | PVG süitleri yeşil. phase1 kırmızı; bu, `main`'de de bilinen SCE kaynaklı kırmızı. |
| WP-0230-DL2A | [#215](https://github.com/bestepullukcu-alt/ERP-vNext/pull/215) | `ad46ea552`, 2026-10-23 12:44 UTC | bestepullukcu-alt (APPROVED) | `pvg-api` önce `CS0535` ile kırmızı oldu. Düzeltme `95a0bb23e` ile yeşile döndü; düzeltmenin kararı `docs/records/decisions/2026-10/pvg-mod0230-dl2a-kapsam-01.md`. Beş PVG süiti yeşil, phase1 aynı bilinen kırmızı. |

Bağımlılık: DL2A ve DL3A aynı arayüzü paylaşıyordu. DL3A önce birleşti, DL2A da onun üstüne düzeltilerek birleşti. Sonraki iş paketi bekleyen bir bağımlılık bırakmadı.

## Entegrasyon kapısı ölçümü: PASS

Ölçüm CT dalında yapıldı. Dal, `main` `ad46ea552` ile birleştirildi: merge commit `ebcc124d1`. Bu dalda `services`, `gateway` ve `tests` yolları `main` ile aynı (`git diff --stat origin/main HEAD -- services gateway tests` boş).

| Süit | Sonuç | `known_failures.py check` |
|---|---|---|
| `Diten.PvgService.RegPvBase.Tests` | 97/97 | `pvg-regpv-base`: tüm sayaçlar 0 |
| `Diten.PvgService.Api.Tests` | 58/58 | `pvg-api`: tüm sayaçlar 0 |
| `Diten.ApiGateway.Tests` | 313/313 | `gateway`: tüm sayaçlar 0 |

Sonuç:

- DL2A'nın 7 testi, DL3A'nın 8 testi ve DL8'in iki Gateway testi birlikte `main` kodunda yeşil.
- `pack_approval.py check` MOD-0230 sonucu çıkış 0.
- `ct_queue.py validate --queue pvg` sonucu 0 hata, 0 uyarı.

## Kabul engeli (ölçüldü)

`scripts/acceptance_record.py draft --part list --queue pvg --wp WP-0230-DL2A --pack <MOD-0230>@ebcc124d1` komutu şu yanıtı verdi:
"DUR: taslak üretilemedi — pakette AC kimliği yok (eski biçim) — AC satırları taşınamaz".

Neden:

1. Kabul aracı, paketteki ölçütleri yalnız `- [ ] AC-<n> (zorunlu|isteğe bağlı) — …` biçiminde tanıyor.
2. MOD-0230 paketi ise ölçütlerini `- [ ] BG-<n> (DL-…) - …` biçiminde yazıyor; bu biçimde 33 madde var.
3. `execution/approvals/POLICY-DISPATCH-REVIEW-START.md` var. Bu durumda bir ürün kaydı `module_pack`'i bağlamak zorunda, ve bağlanan paket "eski biçim" olduğu için kontrol ENGEL veriyor.

Sonuç: Bugünkü araçla MOD-0230'un hiçbir iş paketi `ACCEPTED` olamaz. Bu, bu dalganın kod ya da kanıt sorunu değil; paket biçimi ile kabul aracı arasındaki bir kurulum uyuşmazlığı. Satırlar `INTEGRATED` kalır. Çözüm yolu sahibe soruldu.
