# MOD-0178 DL-6 — Control Tower kabulü

- Tarih: 2026-11-06. CT: MVP-5 CT (Beste, merkez), oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1`
  (sabotajlar `81546c373`'te; tam takımlar dal ucu `6a1f3368a`'da).
- İncelenen: `cbd35f4af` (feat(web): MOD-0178 DL-6 S2 screens — Outbound Shipments (Compact), Pick Tasks (Slim), Packing; manifest, 7 languages,
  page help), ebeveyn `2aab920f1` (DL-5 CT F-1 / F-2 testleri). Dal ucu `81546c373` (CT kayıt merge'leri), sonra `6a1f3368a` (MVP-1 ortak
  arayüz kuralları, aşağıda ayrı bölüm). MVP-1 hattında; push / PR yok. Geliştirme raporu `mod-0178-dl6-01-rapor.md` (oturum `local_75b9d993`).
- Paket: `MOD-0178-putaway-picking-packing.md` r25 @ sha256 `b24ff825…6de4`, onay satırı `execution/approvals/approvals.tsv:46` (HIGH).
- Sonuç: **VERIFIED** — kapsam temiz, CT'nin dört DL-6 sabotajı ve iki CSS geri alma sabotajı kırmızı, tam takımlarda yeni kırmızı yok.
  ACCEPTED iki kapıya bağlı: AC-76 Auth kapısı (Ali) ve canlı E3.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`2aab920f1..cbd35f4af`) | 90 dosya: Inventory 2 (`PutawayPickingPackingManifestProvider.cs`, `WarehouseRegistrationTests.cs`); Web proxy 3 controller + 3 model; Views 22 (S2 + S1 PutawayTasks / WarehouseLocationRoles'da F-7 işaretlemesi ve köken alanı — MOD-0178'in kendi S1 ekranları); wwwroot 13 (Warehouse JS / CSS); Resources 42 (S2 aileleri 7 dil, PutawayTasks +8 anahtar, `SharedResource` +3 `Nav.Page.*`, `PageHelpResource` +32); `PageHelpCatalog.cs`; Web.Tests 4 |
| Korunan yollar / XMC | `ocelot.json`, `_Layout.cshtml`, `.antigravity/**`, başka modülün dosyası **yok**. Paylaşılan dosyalar (`SharedResource`, `PageHelpResource`, `PageHelpCatalog.cs`) yalnız eklemeli |
| `2aab920f1` | Yalnız iki Inventory test dosyası; üretim kodu değişmedi (DL-5 kaydında "F-1 / F-2 kapanışı") |

## Tam takımlar (filtresiz; `INVENTORY_TEST_MONGO` yok, `DITEN_TEST_MONGOD` = mongod 9.0, 27017 yok; AccountKindAcceptanceHost.Tests koşulmadı)

| Takım | `6a1f3368a` (CT) | Geliştirici (`cbd35f4af`) | Taban `7bd1d0bae` (CT) | Not |
|---|---|---|---|---|
| Inventory | **1900 / 1905, 5 atlandı, 0 kırmızı** | 1900 / 0 / 5 | 1898 / 1903, 5 atlandı | +2 = F-1 / F-2 testleri |
| Web | **4357 / 4359, 2 kırmızı** | 4357 / 2 (4359) | 4263 / 2 (4265) | `CapacityPlansPageTests` + `SandopPlansPageTests` `A_failed_or_denied_first_load…` (MVP-6, taban); `81546c373`'te de aynı |
| Gateway | **367 / 368, 1 kırmızı** | 367 / 1 | 367 / 1 | `Mod0173Pm2InternalInventoryRouteTests…`, taban |
| TenantArchitecture | **222 / 227, 5 kırmızı** | 222 / 5 | 222 / 5 | `DocsPathGuardTests` ×5, taban |
| TenancyTests | **3 / 3** | 3 / 0 | 3 / 0 | |
| Web JS (`npx vitest run`, tam) | **5395 / 5433, 38 kırmızı** | 38 | `81546c373`: 5387 / 5430, 43 kırmızı | 5 düzeldi, yeni kırmızı 0 (aşağıda) |

Not: `81546c373`'teki ilk Inventory koşusu ortam nedeniyle bozuldu (1347 kırmızı, hepsi fixture kurucusunda "MongoDB connection availability took
longer than 30 seconds" — kendi replica set'i yükte açılamadı); aynı Inventory kodu `6a1f3368a`'da yeniden koşuldu, 0 kırmızı.
Diğer servisler (Auth, CRM, MDM, Platform, SupplyChain) `7bd1d0bae`'den beri bayt-eşit; sonuçları `7bd1d0bae` koşusundan (ayrı rapor).

## CT sabotajları (uygulandı → sonuç → geri alındı; hepsi `81546c373`, sha256 eşit, ağaç temiz)

| # | Sabotaj | Sonuç |
|---|---|---|
| D6-1 | Web `OutboundShipmentsController.Cancel`: `OutboundShipmentsCancel` izin kontrolü silindi (yazma eylemi) | kırmızı: `WarehouseProxyActionKeyTests.Every_proxy_action_without_its_own_key_is_403_and_never_reaches_the_gateway(action: "OutboundShipments.Cancel")` (Web Warehouse 208 / 209) |
| D6-2 | `PackingIndex.ar.resx`'ten `Warehouse.Package.Action.Close` silindi | kırmızı: `WarehouseS2ScreensTests.AC34_the_family_has_one_key_set_in_all_seven_languages_and_no_english_left_behind(folder: "Packing")` |
| D6-3 | Manifest'ten `PICK_TASK_CLAIM` eylemi silindi | kırmızı: Inventory `WarehouseRegistrationTests.AC34_the_manifest_declares_the_three_S2_pages_and_one_action_per_S2_button` (332 / 333) |
| D6-4 (F-7) | `warehouse.css` `.wh-touch` kuralından `min-block-size` / `min-inline-size: 48px` kaldırıldı | kırmızı: `WarehouseScreensSurfaceTests.AC45_the_confirm_panel_has_48px_touch_targets…` ve `WarehouseS2ScreensTests.AC45_F7_F8_every_close_button_is_a_48px_target…` |
| D6-5 (F-8) | `warehouse.css` `.wh-list .dropdown-menu .dropdown-item`'dan `white-space: normal` kaldırıldı (geliştiricinin sabotajından farklı satır) | kırmızı: `WarehouseS2ScreensTests.AC45_F7_F8_…` |

Geliştirici ölçümleri (CT tekrar koşmadı): 22 / 22 DL-6 sabotajı kırmızı; `verify_datatable_page` OutboundShipments 104 kontrol 0 sapma, PickTasks
§11.5 bilinçli sapma; 375 px DOM ölçümü × 72 × 72, satır menüsü sağ kenar 343 ≤ 375 (oturumsuz düzenekte).

## Bulgular

- Yok (ENGEL 0, ORTA 0). Bilgi: S1 ekranlarında `verify_datatable_page`'in 1'er boşluğu ("Form declares a strongly typed ViewModel") DL-3
  dosyası, tabanda da var — takip önerisi (DÜŞÜK, S-101).

## MVP-1 ortak arayüz kuralları (6a1f3368a)

- Kapsam: 20 dosya, yalnız Web + JS testleri (`SharedResource.*` 7, `RoleAssignmentsIndex.*` 7, `_IndexL10n.cshtml`, `esign-pages.js`,
  `perm-label.js`, üç `tests/*.test.js`). Servis, `ocelot.json`, `_Layout.cshtml`, `.antigravity/**` yok. SupplyChain satır adları (MVP-6)
  bilinçli olarak dokunulmadı (XMC-001).
- Tam JS takımı: `81546c373` 43 kırmızı → `6a1f3368a` **38** (geliştiriciyle aynı). Düzelen 5: `global-confirm-input-type` (1),
  `global-confirm-text-is-text` (2: "none escapes first", "only through subtextHtml"), `role-assignments-label-ledger` (2: fiiller).
  Yeni kırmızı yok. Kalan 38'in hiçbiri bu commit'in dosyalarında değil (çoğu CRLF / ortam; `role-assignments-label-ledger` "every row"
  SupplyChain'in üç satırıyla kırmızı kalıyor — MVP-6).
- Yorum atlama sabotajları (`tests/global-confirm-text-is-text.test.js`, her biri `git checkout` ile geri alındı, sha256 eşit):

| # | Sabotaj | Sonuç |
|---|---|---|
| V-1 | `commentEnd` her zaman `-1` döner (tarayıcı yorumları atlamaz) | kırmızı: "pass a body of markup only through subtextHtml" — beklenen listeden `Quarantine/blocked.js` ve `Traceability/Lots/status.js` düşer (5 ≠ 7) |
| V-2 | Listede olmayan `Governance/UserRoleAssignments/index.js` çağrısına kesme işaretli yorumun ARDINDAN gerçek `subtextHtml:` eklendi (`/* the dialog's body */ subtextHtml: L.Body,`) | kırmızı: liste 8 ≠ 7, `+ UserRoleAssignments/index.js` — yorumdan sonraki gerçek kullanım yakalanıyor |
| V-3 | Aynı yere `subtextHtml` yalnız yorumun İÇİNDE (`/* subtextHtml: L.Body, it's off */`) | yeşil (38 / 38) — yorum kod sayılmıyor |

## Açık kalanlar

- **AC-76 (a)–(d)** Auth kabul kapısı (Ali): `WarehouseOperator` / `WarehouseSupervisor` / `ShippingPlanner`, Viewer dışlaması.
- **E3 (canlı):** Gateway 5000 üzerinden oturumdaki kullanıcıyla DL-4 / DL-5 / DL-6 akışı (geliştirici raporu §11'deki 9 adım), 375 px × ve satır
  menüsünün canlı ölçümü. MVP-6 canlı değilse Shipped adımı §7 canlıya çıkış kapısına bağlı.
- MOD-0177-FU01 F-1 (GUID etiketli Legacy satır testi) FU01 geliştiricisine devredildi (XMC-001) — bu dilimin işi değil.
- DL-7 (S3) sırada.

ACCEPTED, kod main'e girdiğinde ve iki kapı (AC-76, E3) kapandığında yazılır (SOP §7).
