# MOD-0181 DL-4 — CT kabulü (ekranlar: Sayımlar Panosu + KPI, Sayım Formu, Fark İzi; 7 dil, yardım, manifest; DL-3 taşınanları)

- Tarih: 2026-11-11. CT: MVP-5 WMS CT (oturum `ca7382cf`). Talimat: WP-0181-DL4-01. Sahip: Beste.
- Hedef: hatta `96dafb449..e9946f076` — `ee2538b6d` (origin/main `290134f8f` merge), `a46ec0952` (sözleşme taslağı: pano), `88054ac8a` (servis:
  pano KPI, manifest, taşınan 10–13, Gateway 69 → 70), `e0bf26918` (Web: üç ekran, proxy, 7 dil, nav, yardım, testler), `e89891108`
  (birincil düğme işaretleri), `e9946f076` (SKU testi tek başına).
- Sonuç: **VERIFIED** (kod ve testler). **ACCEPTED değil** — iki kapı açık: canlı ölçüm (AC-39 375 px, AC-38 yetkisiz yüzey, `ar` ekranı;
  filo kapalı, kiracı girişi sahipte) ve paket §24 DL-4 kabul kapısı AuthService rol şablonu (Q-4 = A; Ali, MOD-0018-FU31).

## Tam takımlar (CT, `e9946f076`, süzgeçsiz; `INVENTORY_TEST_MONGO` yok, 27017 kullanılmadı; Platform için `DITEN_TEST_MONGOD`)
| Takım | Sonuç |
|---|---|
| Inventory | 2087 / **1** / 5 atlandı — kırmızı `OutboundRecoveryTests.AC71_ii_a…` (MOD-0178); tek başına 3 koşuda 3/3 yeşil; DL-4 servis tarafında yalnız sayım dosyalarına dokunuyor → kararsız test, DL-4'ün değil (DÜŞÜK-1) |
| Gateway | **371 / 0** (70 rota, sahip «70 yap» 2026-11-11) |
| TenantArchitecture | 222 / 5 — `DocsPathGuardTests` ×5 (main tabanı) |
| Tenancy | 3 / 0 |
| Platform.Application | 9140 / 19 — Mongo ortam testleri (DL-3 kabulündekiyle aynı 19) |
| Web | 4449 / 2 — `CapacityPlansPageTests`, `SandopPlansPageTests` (bilinen) |

Geliştiricinin ölçümleri (rapor): vitest 13 kırmızı = origin/main; `verify_datatable_page.py` iki modül PASS; `check_pack_nav_keys.py` 0 yeni;
UIC çıkış 0; resx-check main ile aynı; 19 sabotajın hepsi kırmızı, bayt-eşit geri.

## CT sabotajı
| # | Sabotaj | Sonuç |
|---|---|---|
| CT-1 | `ResolveCountAdjustmentIntegrityHandler.cs:113` kalem (`ItemId`) karşılaştırması silindi | **kırmızı** — `CycleCountRecoveryTests.AC26_ORTA2_a_holder_of_another_item_is_not_accepted` (1/168); bayt-eşit geri |

## DL-3'ten taşınanlar
ORTA-1 (manifest eşitlik testi), ORTA-2 (AcceptHolder SKU + kalem), DÜŞÜK-1 (tolerans sınırı), DÜŞÜK-2 (MaxRecounts karışık derinlik),
DÜŞÜK-3 (Title Case) — geliştirici sabotajlarıyla kapandı. **Paket metni:** ORTA-2'nin paket §8.3 / §12 / AC-26 metni hâlâ r7; MOD-0181 r8
(CT) bekliyor (sahip kararı «Ekle» 2026-11-10).

## Bulgular
- **DÜŞÜK-1:** MOD-0178 `OutboundRecoveryTests.AC71_ii_a…` kararsız (tam koşuda 1/4 kırmızı) — MOD-0178'in sonraki diliminde zamanlama bağımlılığı incelensin.
- **DÜŞÜK-2:** Rapor kısmen İngilizce; talimat "Rapor dili: Türkçe" — sonraki talimatlarda vurgulanır.
- **Brief ↔ paket (paket kazandı, uygun):** Layout yalnız sayfa görünümlerinde (partial'da çift kabuk olurdu; MOD-0178 AC-21 emsali); "Toplu Sil"
  (`counts.bulk-delete`); `scanRef` yalnız bulunan stok panelinde (AC-46); `Note.ManualCount` paylaşılan yardım paneli `Note.*` çizmediği için
  Step1'e de yazıldı (paneli değiştirmek XMC).
- **Brief'ten eksik (açık, sahip kararı):** gönderimde "sayılmamış satırlara git"; Fark İzi'nde "başlangıçta beklenen" / "sayıma kadar hareket"
  sütunları ve "seri" işareti.
- **Sözleşme:** WMS-COUNT-BUNDLE hâlâ taslak (dondurma CT adımı sonra).

VERIFIED
