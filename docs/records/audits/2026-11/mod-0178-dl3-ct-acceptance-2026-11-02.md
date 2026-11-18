# MOD-0178 DL-3 — Control Tower kabulü (WMS-MVP5-DL3)

- Tarih: 2026-11-02. CT: MVP-5 CT (W-10), oturum `ca7382cf`. Doğrulama: CT içi alt ajan (Opus 5.5 · high), depo dışı detached ağaçlar
  (`a98e9c54e` ve taban `618929f7d`), iş sonunda kaldırıldı. Geliştirme oturumu `local_da5462be`, aktaran `local_75b9d993`.
- İncelenen: `8f972b86a` (DL-2 F-3 testi) · `db6127ba5` (manifest: PUTAWAYTASKS + WAREHOUSELOCATIONROLES, §11.3'ün 14 eylemi) ·
  `a98e9c54e` (iki Golden Slim ekran, 7 dil, sayfa yardımı). MVP-1 hattında (`feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok.
- Paket: `MOD-0178-putaway-picking-packing.md` @ sha256 `9b4be5e3…1decf` (r19, onaylı).
- Sonuç: **KOŞULLU** — engel yok; F-1 ve F-2 kapanmadan, F-3 DL-8 sonrasına açık AC olarak yazılmadan VERIFIED yazılmaz.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`618929f7d..a98e9c54e`) | 62 dosya MOD-0178 (Web, Web.Tests, Inventory manifest, 2 test) + 2 doküman dosyası `7e5d7ca7a`'dan (MOD-0040-FU02); `ocelot.json`, `_Layout.cshtml`, `.antigravity`, başka modül servisi yok |
| Derleme | Inventory 0 hata / 0 uyarı (taban 0 / 0); Web 0 hata, uyarılar tabanla birebir |
| Tam Inventory takımı (filtresiz, kendi replica set'i) | 1578 / 1582, 4 atlandı, 0 kırmızı |
| Web takımı | 4180 / 4184; 4 kırmızının dördü tabanda da kırmızı (MRP / SafetyStock nav, Capacity, S&OP); yeni kırmızı 0 |
| Repo mimari testleri | 10 kırmızı, tabanla ad ad aynı (approvals-append-only.yml kaynaklı CiServiceTestsGuard 3, DocsPathGuard 5, AuditTrail 2); Inventory 0 |
| 7 dil | PutawayTasksIndex 206, WarehouseLocationRolesIndex 104 anahtar; yeni 22 yardım + 3 nav anahtarı; eksik / boş / İngilizceyle aynı 0; hardcoded metin yok |
| Manifest ↔ controller | 14 eylem bire bir; Web proxy'leri gövdede `_permissions.Has(...)` (repo emsali), otorite servis `[HasPermission]` |
| UAS-001 | yetkisiz kullanıcıya `_AccessDenied`, yönlendirme ve iskelet yok |
| AC-22 doğrulayıcı | paketteki komutla FAIL (varsayılan direct-gateway profili); `--api-profile proxy` ile iki ekran 104 kontrol, 0 sapma |

## CT sabotajları (uygulandı → sonuç → byte-eşit geri alındı)

| Sabotaj | Sonuç |
|---|---|
| F-3: toplu atamaya eski `IntegrityError` dalı | kırmızı (`/results/0/outcome` enum ihlali) — DL-2 F-3 kapandı |
| `PutawayTasksController.Confirm` izin kontrolü silindi | kırmızı |
| `PutawayTasksController.Edit` izin kontrolü silindi | **yeşil (tam Web takımı) — F-1** |
| `WarehouseLocationRolesController` Edit + BulkDelete izin kontrolü silindi | **yeşil — F-1** |
| `SharedResource.ar.resx`'ten `Nav.Page.PUTAWAYTASKS` silindi | kırmızı |
| Manifestten `PUTAWAY_TASK_CLAIM` silindi | kırmızı |
| Yetkisiz daldan `return;` kaldırıldı (UAS-001) | kırmızı |

## Bulgular

F-1 | ORTA | Web proxy yazma eylemlerinin izin kontrolü testsiz: PutawayTasks.Edit ve WarehouseLocationRoles.Edit / BulkDelete'ten izin kontrolü silinince tam Web takımı yeşil; eylem başına anahtar testi Edit, rol Edit / BulkDelete, Export, Warehouses, Bins, People, Nodes ve rol ayrıntısını kapsamıyor | açık | MOD-0178 geliştirici | 2026-11-04
F-2 | ORTA | §11.3a'nın iki ekran durumu (saklama koşulu bilinmiyor rozeti + pasif hedef; şirket kodu geçersizken pasif Yeni görev) yok, yalnız gönderim sonrası hata; kök neden aday DTO'sunda storageConditions yok ve şirket kodu ön okuması yok | açık | MOD-0178 geliştirici + sahip kararı | 2026-11-04
F-3 | ORTA | AC-45 (375 px, 48 px hedef, ekran görüntüsü + DOM ölçümü) ve §17 E4 çalışma zamanı kanıtı yok; Gateway rotası (DL-8) gelince koşulur | açık | integration-agent + MOD-0178 geliştirici + CT | DL-8 sonrası
F-4 | DÜŞÜK | AC-22 doğrulayıcı komutuna --api-profile proxy eklenmeli | açık | paket sahibi | sonraki paket revizyonu
F-5 | DÜŞÜK | §11.2 Slim seti listesinde rules.js ve warehouse.css yok | açık | paket sahibi | sonraki paket revizyonu
F-6 | DÜŞÜK | Raf önerisi yalnız düzenleme formunda (sözleşmede adayda öneri yok); §8.6 / AC-42'ye sahip onaylı not | açık | sahip kararı + paket sahibi | sonraki paket revizyonu

## Açık kalanlar

- WMS-BUNDLE dondurması (§19.7 / AC-60) DL-3 dışında; dondurma sonrası Web DTO'ları yeniden kıyaslanır.
- Geliştiricinin 14 mimari kırmızısına karşı CT ağacında 10: fark `.claude/worktrees` kopyalarının taranmasından (CT ağacı depo dışında; ölçülmedi).
- Taban kırmızıları DL-3 bulgusu değil; sahiplerine bildirim ayrı takip.

## Düzeltme turu ve sonuç (2026-11-02, aynı gün)

- Düzeltme commit'leri: `bec38513f` (F-1, yansımayla eylem başına anahtar testi) · `03aa826dd` (F-2(1) servis: aday cevabında
  `storageConditions`) · `dc03e189b` (F-2(1) ekran: "Saklama koşulu bilinmiyor" rozeti, hedef raf ve Kaydet kapalı, 7 dil). Geliştirici:
  Inventory 1579 / 0 kırmızı; Web 4233 geçti, 5 kırmızı tabanda (4 CT taban listesi + `WorkCenterMenuWithoutPermissionsTests` ortam);
  mimari kırmızılar tabanla aynı.
- CT ölçümü (depo dışı detached ağaç `dc03e189b`, iş sonunda kaldırıldı): Web depo testleri 115 / 115; CT sabotajı
  `PutawayTasksController.Edit` izin kontrolü silindi → 1 kırmızı (F-1 kapandı); aday işleyicisinde `StorageConditions` ataması
  kaldırıldı → `PutawayCreateRulesTests` 1 kırmızı (F-2(1) kapandı); ikisi de byte-eşit geri alındı.
- F-2(2) (şirket kodu ön okuması): sahip kararı **C** (2026-11-02) — paket ve taslak ön okuma ucu tanımlamıyor; gönderim sonrası 7 dilli
  `WMS_COMPANY_CODE_INVALID` yeterli. Kayıtta açık nota dönüştü (paket revizyonunda §11.3a metni hizalanır).
- F-3 (AC-45 375 px + §17 E4 çalışma zamanı kanıtı) açık AC olarak kalır: DL-8 hatta (`83b01ef62`), Web 5001 bu sırada cevap vermedi;
  CT canlı ölçümü Web ayağa kalkınca koşar.

**Sonuç: VERIFIED** — F-1 ve F-2(1) kapandı; F-2(2) sahip kararıyla not; F-3 açık AC (DL-8 sonrası canlı ölçüm). ACCEPTED, kod main'e
girdiğinde yazılır (SOP §7).

## F-3 canlı ölçümü — AC-45, 375 px (2026-11-05)

- Ortam: fleet açık (Web 5001 → Gateway 5000 → Inventory 5067, ana checkout `d6aff589e`), tarayıcı 375 × 812 (mobil öykünme), kiracı kullanıcısı
  gerçek oturumla; kiracı dev JWT yok. Şirket GMG, depo D01.
- Test verisi (hepsi bu ölçüm için, kullanıcı onayıyla): MDM raf KBL-01 ve A-01-01-02 (D01-A altında, «ekle»); KBL-01 Mal Kabul rolü;
  `COUNT_GAIN` INV-2026-0000016 (5 EA, lot AMX-2604, KBL-01); görev **GMG-PUT-000001** (KBL-01 → A-01-01-02, 5 EA). Görev onaylanmadı
  (ölçüm onay gerektirmez; açık kalır).
- **Önkoşul bulgusu:** ilk deneme 422 `WMS_COMPANY_CODE_INVALID` — GMG tüzel kişilik kodu `GMGGRANDMEDCALLACLARLTDST-238`,
  `WarehouseRecordCodeRules.IsValidCompanyCode` `^[A-Z]{2,5}$` ister. Kiracıdaki tüzel kişiliklerin hiçbirinin kodu kurala uymuyor (`LE-001`,
  `SETONDATR-274` …). Kullanıcı kararıyla (Beste, 2026-11-05) yalnız GMG kodu MDM'de `GMG` yapıldı (sürüm 1 → 2; ülke `TR` zaten kayıtlıydı,
  onay / yaşam döngüsü durumu değişmedi). Geri kalan kodlar MDM sahibine (Ali) iletilecek. Yan bulgu (MDM, XMC-001 — dokunulmadı):
  tüzel kişilik sihirbazının ülke listesi `GET …/reference-data/country` 400 `scope_key_required` alıyor ve boş kalıyor; düzenlemede
  kayıtlı ülke görünmüyor ve kaydetme "Bu alan zorunludur." ile duruyor.
- Ölçüm (DOM, `getBoundingClientRect`; ekran görüntüleri `mod-0178-dl3-375px/`):

| Öğe | Ölçü | AC-45 |
|---|---|---|
| Panel (`offcanvas-end wh-confirm-panel`) | 375 × 812, yatay kaydırma yok | — |
| Miktar − / + (`aria-label` "Miktarı azalt / artır") | 57 × 48 | ✓ |
| Miktar alanı `confirmQuantity` | 231 × 48 | ✓ |
| Gerçek hedef raf `confirmBinCode` | 343 × 48 | ✓ |
| Onayla `btnConfirmSubmit` | 351 × 48; ebeveyn `wh-action-bar` `position: sticky; bottom: 0` | ✓ sabit alt çubuk |
| Panel kapatma (×, `aria-label` "İptal") | **36 × 36** | ✗ |

F-7 | DÜŞÜK | AC-45: onay panelinin kapatma düğmesi (×) 36 × 36 px, 48 px hedefin altında; öbür dokunma alanları, −/+ ve sabit alt çubuk ölçüldü | açık | MOD-0178 geliştirici | sonraki Web dilimi (DL-5)
F-8 | DÜŞÜK | 375 px'te liste satır menüsü sağdan taşıyor: "Kalanı Kapat — Henüz işlenen yok — İptal kullan…" maddesi kesik (`row-menu-375.jpg`) | açık | MOD-0178 geliştirici | sonraki Web dilimi (DL-5)

**F-3: AC-45 kolu kapandı** (canlı ölçüm yapıldı; iki DÜŞÜK bulgu F-7 / F-8 dev talimatına girer, S-101). §17 E4'ün 375 px dışındaki
maddeleri bu ölçümde koşulmadı.
