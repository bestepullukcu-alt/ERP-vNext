# WP-0178-FU01-DL1 — MOD-0178-FU01 Çıkış sevkiyatı formu v2: satır tablosu + satır paneli + önizlemede seçilen lot (yalnız Web)

- Kimlik: **WP-0178-FU01-DL1**. Modül: **MOD-0178-FU01 — Outbound Shipment Form v2 (Çıkış sevkiyatı formu v2)**, ebeveyn MOD-0178;
  ekran `frontend/Diten.Web` (Warehouse / OutboundShipments), kiracı modülü (`_LayoutTenantShell`, 7 dil).
- Paket: `execution/domains/supply-chain-execution/module-packs/MOD-0178-FU01-outbound-form-v2.md` — **r4**, LF sha256
  `949ab3c997e65d59f0ba5579c3a3b6d3a796642675f991014c789f012ce711ca`, `status: ready-for-dev`.
- Onay: `execution/approvals/approvals.tsv` satırı (`:89`; onaylayan Ali / `GMG-ERP`, 2026-11-13, yazan `bestepullukcu-alt`) — commit `ac493e735`.
  Not: künyedeki `approval_status: pending` (`:17`) ve `status_note`'taki "onay satırı bekler" künyenin onaydan önceki hâlidir; yetki kaynağı
  onay satırıdır (WP-0180-DL1 ile aynı durum). Bağımsız incelemeler: r3 UYGUN, 4 düşük bulgu
  (`docs/records/reviews/MOD-0178-FU01/2026-11-13-e7d69699.md`); r4 UYGUN, bulgu yok (`docs/records/reviews/MOD-0178-FU01/2026-11-13-949ab3c9.md`).
- Dilim: **DL-1** (paket §23 `:971`). Kanıt seviyesi: **E4** (§23 DL-1 satırı).
- Sahip: geliştirici oturumu (MVP-5). CT doğrulayıcı: **MVP-5 WMS CT** (sahip Beste).
- Durum: **ready**. Bu WP push, PR, servis / sözleşme değişikliği ya da `.antigravity/**` değişikliği izni değildir.
- Yetki: paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. Ebeveyn
  `MOD-0178-putaway-picking-packing.md`'nin davranış kuralları (§8.17 tahsis, §8.18 durum makinesi, §11.5 S2 dosyaları) aynen geçerlidir
  (paket `:73-77`). WP ile paket çelişirse paket geçerlidir; çelişkiyi CT'ye yaz, kendin çözme.

## 0. Adım 0 — çalışma dalını hat ucuna getir

1. Ana checkout `C:\Users\Ali\erp-vnext`; çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`** (MVP-1 hattı). Yeni dal açma.
2. `git status --porcelain` ile çalışma ağacını ölç. **CT kararı (S-5):** commit'lenmemiş değişiklik yalnız MOD-0180 oturumunun yollarındaysa
   (`services/Diten.InventoryService/**`, `docs/analysis/contracts/drafts/wms-wave-bundle-*`, `gateway/**` testleri) devam et — onlara
   dokunma, stash'leme, geri alma. Bu WP'nin dosyalarında (`frontend/Diten.Web/**`, `frontend/Diten.Web.Tests/**`) commit'lenmemiş
   değişiklik varsa **dur**, CT'ye bildir.
3. Etiket `ct/line-ff-2026-11-13e`: `git merge-base --is-ancestor ct/line-ff-2026-11-13e HEAD` doğruysa dal etiketi zaten içeriyor — **devam et**. Değilse `git merge --ff-only ct/line-ff-2026-11-13e`. ff mümkün değilse (aynı dalda MOD-0180 oturumu etiketten sonra commit atmışsa) **CT kararı:** `git merge --no-ff ct/line-ff-2026-11-13e -m "merge: CT chain ct/line-ff-2026-11-13e into MVP-1 line (CT standing approval, docs/records only)"`; etiketin getirdiği yalnız CT kayıtlarıdır (paket, karar, inceleme, onay satırı, WP). Çakışma çıkarsa `git merge --abort` ve **dur**, CT'ye bildir; rebase yok.
4. **Aynı checkout'ta ikinci oturum:** MOD-0180 DL-1 geliştirme oturumu (WP-0180-DL1) aynı dalda arka uç (`services/Diten.InventoryService/**`)
   üzerinde çalışıyor. Bu WP **yalnız** `frontend/Diten.Web/**` ve `frontend/Diten.Web.Tests/**` altındaki §3 dosyalarına dokunur.
   - Stage **yalnız açık yolla** ve yalnız bu WP'nin dosyaları; karşı oturumun dosyalarını (InventoryService, `docs/analysis/contracts/drafts/**`,
     Gateway testleri, `SharedResource.*.resx` `Perm.Entity.*` satırları — WP-0180-DL1 X-15) **stage etme, düzenleme, geri alma**.
   - Ortak bir dosya gerekirse (ör. `frontend/Diten.Web/Resources/SharedResource.{7}.resx`, `PageHelpResource.{7}.resx`,
     `Services/Help/PageHelpCatalog.cs`) **önce CT'ye sor** (§10 S-1).
   - Fleet `dotnet watch` ana checkout'tan koşuyorsa derleme, test ve sabotaj koşularını ayrık (detached) bir worktree'de yap; ana checkout'ta
   `frontend/Diten.Web/node_modules` yoksa vitest için başka bir worktree'nin `node_modules`'una junction kullanılabilir (kaldırırken yalnız junction'ı `rmdir`).
5. Dilim başı ölçümleri (§7) bu commit'te alınır ve teslim raporuna yazılır.

## 1. Amaç ve kapsam

Paket §23 DL-1 (`:971`): "satır tablosu + yan panel + önizlemede seçilen lot + Edit salt okunur tablo + Details'te FEFO gerekçesi ayrı sütun
(**yalnız Web; servis ve sözleşme değişmez**)". Kapsadığı bölümler: §8.1 (`:287-300`), §8.2 (`:302-327`), §8.3 (`:329-357`), §8.6 (`:518-526`),
§9 (`:558-573`), §11 (`:590-615`), §12 (`:617-637`, DL-1 satırları), §13 (`:639-661`, DL-1 satırları), §25 (`:989-997`).
Sahip kararları: D-1, D-4, D-5 (`docs/records/decisions/2026-11/mod0178-fu01-outbound-form-v2-owner-decisions-01.md:12`, `:15`, `:16`);
Q-8 = kabul (aynı dosya `:75-76`, commit `bc503498c`).

**DL-1'de yapılacaklar (paketten):**
- Create "Satırlar" kartı: bugünkü kart listesi (`_Form.cshtml:83-92`, `form.js:75-91` `lineRow`) yerine
  `<table id="outbound-lines-table" data-dt-standard="v2" data-dt-data-mode="client">`; kaynak bellekteki satır dizisi; üst sınır **100**
  (`wms-bundle.openapi.yaml:1989` `maxItems: 100`); sütun sırası, durum rozetleri (ikon + metin), araç çubuğu, satır menüsü (Düzenle, Lot
  değiştir, Sil — yerel, `window.showConfirm`), boş ve yükleniyor durumu (§8.2 `:306-317`). Emsal istemci modu tablo:
  `frontend/Diten.Web/Views/CRM/CyclePeriods/_DataTable.cshtml` (ölç, kopyalama).
- **Yeni** `_LineOffcanvas.cshtml`: 6 alan (ürün, SKU, miktar, sipariş satırı no, lot, FEFO sapma gerekçesi), her alan `.diten-field` + ikon,
  başlıksız Golden Slim deseni (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/_CreateEditOffcanvas.cshtml`); düğmeler
  "Satırı önizle" (`btn-label-primary`), "Satırı kaydet" (`btn-primary`, `data-dt-primary="enter"`), "Vazgeç" (§8.3 `:333-343`). Partial;
  `Layout =` taşımaz (§9 `:561`).
- Panel önizlemesi (tek satır, `lotOverride`'lı) ve tablo önizlemesi (bütün satırlar) — aynı uç `POST /Warehouse/OutboundShipments/preview`;
  seçilen lot gösterilir (D-4); sunucu reddi `details.lineSeq` ile satıra; başlık hataları Staging altında; eskime kuralları; kayıt gövdesi
  bugünkü `CreateOutboundShipmentRequest` (§8.3 `:344-357`).
- FIX-01 davranışı **yeniden yazılmaz, taşınır** (§8.1 `:289-300`): `rules.lineBody` (`rules.js:129`), `rules.lotNeed` (`rules.js:144`),
  `rules.quantityText` (`form.js:33`, `details.js:28`), lot değişiminde yeniden önizleme (`form.js:104-105`), sapma notu (`form.js:181-185`),
  Staging hata eşlemesi (`form.js:209-215`). Yeni biçimleyici yazılmaz.
- Edit: aynı tablo **salt okunur** (araç çubuğu ve satır menüsü yok); bugünkü `outbound-lines-readonly` (`form.js:293-294`) bu tabloya döner;
  FEFO gerekçesi lot hücresinin satır altı metni (§8.2 `:318-325`).
- Details: `details.js` **yalnız** `renderLines` (`:58-71`) — FEFO gerekçesi "Rezervasyon"dan sonra ayrı `AllocFefoReason` sütunu, boşsa "-";
  "Rezervasyon" hücresi yalnız durum (bugün gerekçe aynı hücrede, `:63-64`).
- Sipariş ref.: tek başına `.diten-field`; "siparişten seç" düğmesi / ipucu / boş seçici **çizilmez** (§8.6 `:523-526`).
- Sayfa yardımı (§25 `:991-997`): `Help.OUTBOUNDSHIPMENTS.*` adım 2–3 yan panel ve tabloya göre; adım 4 DL-1'de **yalnız "elle yaz"** (CRM
  seçicisini vaat etmez); `.Immediate` metni; tur yok. Dosya yeri için §10 S-1.
- Yeni resx anahtarları (öneri listesi §11 `:610-615`): `LinesTable.*`, `LineStatus.*`, `LinePanel.*`, `LineLimitReached`, `LotNoLongerEligible`,
  `AllocFefoReason`, satır altı etiketi "FEFO gerekçesi:" — FIX-01 terimiyle ("Çıkış sevkiyatı"), **7 dilde**.

**Kapsam DIŞI:**
- **DL-2** (servis + sözleşme): `customerRef`, `customerAddressId`, `removeCustomerRef`, CRM U-1 adaptörü, üç `WMS_CUSTOMER_*` kodu,
  `OutboundDtos.cs` / `OutboundShipmentsController.cs` gövde geçişi, WMS-BUNDLE / WAREHOUSE-OUTBOUND eklemeli sürümü (CT adımı).
- **DL-3** (CRM seçici): `customers` / `customer-ship-to` proxy'leri, teslimat adresi seçicisi, ülke Reference Data seçicisi, İlçe alanı,
  Details "CRM müşterisi" rozeti / ülke adı / İlçe satırı, Q-12 `customerWarning` bildirimi (`edit/{id}`, `release/{id}`), `Customer.*` ve
  `FieldDistrict` anahtarları, Edit'in değişiklik karşılaştırmasına (`form.js:267`) dört yeni adres üyesi.
- InventoryService, Web denetleyicisi (`OutboundShipmentsController.cs`), `Models/Warehouse/OutboundDtos.cs`, sözleşmeler, Gateway.
- Index liste ekranı (`Index.cshtml`, `_DataTable.cshtml`, `_Filter.cshtml`, `index.js`) — paket dokunmayı planlamaz (§17 `:753-755`).
- `form.js` başlık alanlarının (alıcı kartı) düzeni — DL-1 alıcı kartını değiştirmez; ülke bugünkü gibi serbest metin (§8.4 `:416-418`).

## 2. Ön koşullar (§24 DL-1 satırı `:981`)

- WP-0178-E3-FIX-01 CT kabulü — **kapandı** (`49fd410b7`, VERIFIED; `docs/records/audits/2026-11/mod-0178-e3-fix01-ct-acceptance-2026-11-13.md`).
  Kabul kaydının "Düşük gözlemler" maddesi (Details'te FEFO gerekçesi "Rezervasyon" sütununda) bu dilimin işidir (AC-11).
- Q-8 (Golden sapması G-FU01-01) — **kabul** (Ali, `bc503498c`).
- Seam yok (yalnız MOD-0178 Web dosyaları). FU01 dalı FIX-01'siz tabandan açılmaz (§8.1 `:299-300`); Adım 0'daki etiket FIX-01'i içerir.

## 3. Dokunulacak dosyalar (Repo Scope §5 DL-1 `:229-239`)

`W/` = `frontend/Diten.Web/`.

| Yol | Ne |
|---|---|
| `W/Views/Warehouse/OutboundShipments/_Form.cshtml` | "Satırlar" kartı (`:73-95`): tablo + araç çubuğu, panel partial'ı; sipariş ref. `.diten-field` |
| `W/Views/Warehouse/OutboundShipments/_LineOffcanvas.cshtml` | **yeni** — satır paneli |
| `W/Views/Warehouse/OutboundShipments/Create.cshtml`, `Edit.cshtml` | yalnız partial / betik bağlantıları; `Layout = "_LayoutTenantShell";` (`:12`) ve `PageCode` (`:11`) değişmez |
| `W/wwwroot/assets/js/Warehouse/OutboundShipments/form.js` | satır modeli (bellekte dizi), tablo, panel, önizleme, Edit salt okunur tablo |
| `W/wwwroot/assets/js/Warehouse/OutboundShipments/rules.js` | saf kurallar: satır doğrulama, `lotNeed`, önizleme eşleme, 100 sınırı, sipariş satırı no tekilliği |
| `W/wwwroot/assets/js/Warehouse/OutboundShipments/details.js` | **yalnız** `renderLines` (`:58-71`) |
| `W/wwwroot/assets/js/Warehouse/OutboundShipments/index.l10n.js`, `W/Views/Warehouse/OutboundShipments/_IndexL10n.cshtml` | yeni anahtarların JS köprüsü |
| `W/Resources/Views/Warehouse/OutboundShipments/OutboundShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx` | yeni anahtarlar (7 dil) |
| `W/wwwroot/assets/css/Warehouse/warehouse.css` | yalnız gerekirse panel / rozet sınıfları (yeni CSS dosyası yok) |
| `W/tests/**` | DL-1 vitest dosyaları; `warehouse-s2-e3-fix-01.test.js` FU01 DOM'una **güncellenir, silinmez** (§8.1 `:297-298`, §17 `:740`) |
| `frontend/Diten.Web.Tests/Warehouse/WarehouseS2ScreensTests.cs` | yalnız FU01 DOM'u eski kart listesine bağlı bir doğrulamayı kırarsa; zayıflatma yok, her değişiklik raporda gerekçeli (paket §5 DL-1 listesinde açıkça yok — §10 S-6) |

**Korunan / dokunulmayan (§6 `:257-267`, AGENTS.md §4):**
- `details.js` onay akışları `openCancel`, `sendRelease`, `askReasons`, `openRelease`, `sync` (`:168-245`) — FIX-01 madde 1'in; değişmez.
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` ve Archive — donuk. `_GlobalConfirmation.cshtml` (MOD-0013) değişmez.
- `.antigravity/**` (kullanıcı onayı), `gateway/Diten.ApiGateway/ocelot.json` (yalnız integration-agent), `execution/registries/**`,
  `execution/approvals/**`.
- `docs/analysis/contracts/wms-bundle.openapi.yaml`, `warehouse-outbound.openapi.yaml` (yalnız CT tek yazıcı adımı; DL-1 okur).
- `services/**` (InventoryService dahil), CRM, Auth, MVP-6 dosyaları; başka modülün Web dosyaları. Gerekirse **dur**, XMC-001 ile CT'ye sor.

## 4. Kabul ölçütleri (DL-1 — §16 `:694-708`)

Her guard **üretim koduna karşı** ölçülür; kendi kopyasıyla ölçen test guard değildir (Çekirdek 10).

| AC | Kısa başlık |
|---|---|
| AC-1 | Create satırları `#outbound-lines-table`'da (`v2`, `client`); eski `lineRow` girişleri yok; sütunlar §8.2 sırasıyla; GUID yok |
| AC-2 | Satır paneli 6 alan, `.diten-field` + ikon (`diten-field-icons.test.js` ICON_MAP); birincil `data-dt-primary="enter"`; Enter "Satırı kaydet" |
| AC-3 | Ürün değişince SKU + birim temizlenir, select2 yeniden eşitlenir; `dropdownParent` panel |
| AC-4 | 100 satırda "Satır ekle" pasif + açıklama; 101. satır hiçbir yoldan (panel, klavye) eklenmez |
| AC-5 | Panel önizlemesi `lotOverride.lotId` = seçilen lot; tahsis seçilen lottan, FEFO önerisi ayrıca; sapma kipi + önerilen lot no; canlı kanıt iki lotla (AMX-2604 FEFO 1, G1-20261020 FEFO 2) |
| AC-6 | "Tahsisi önizle" bütün satırlar + her birinin `lotOverride`'ı tek istekte; satır sütunları cevaptan; seçilen lot görünür |
| AC-7 | FEFO kipi istemcide (`Block` / `RequireReason` < 10 / `Warn`); 422 `FEFO_BYPASS_REASON_REQUIRED` / `FEFO_ORDER_VIOLATION` `details.lineSeq` ile doğru satıra |
| AC-8 | Depo / Staging / satır değişince `Önizleme eski`; eski önizlemeye dayanan lot değişimli satır kaydedilmez; listede olmayan seçili lot sıfırlanmaz → `Karşılanamaz` + açıklama |
| AC-9 | Kaydet gövdesi dondurulmuş `CreateOutboundShipmentRequest`'e (WMS-BUNDLE 1.1.0, `additionalProperties: false`) uyar; satır sırası tablo sırası; `lotOverride` yalnız lotlu satırda; `null` yok (§10 S-4) |
| AC-10 | 422 `WMS_ALLOCATION_FAILED` `details.lines[]` satır rozetine + satır altı metnine; outbound yok, yönlenme yok |
| AC-11 | Edit tablosu salt okunur (araç çubuğu / satır menüsü yok); Details'te gerekçe `AllocFefoReason` sütununda, "Rezervasyon" yalnız durum, gerekçesizde "-"; Edit'te gerekçe lot hücresi satır altı metni |
| AC-12 | Tablo ve panelde miktar FIX-01 biçimleyicisiyle (tr: "2.000" → "2", "1.500" → "1,5"); ikinci biçimleyici yok |
| AC-13 | UAS-001 (`.create` yoksa yalnız açıklama), resx 7 dil temiz, 375 px tablo + panel yatay kaydırmasız (`scrollWidth`), sayfa yardımı güncel (§10 S-1, S-2) |

## 5. Sabotaj listesi (§24 DL-1 satırı `:981` — tam liste, 14 kalem)

Her sabotaj: değişiklik + kırmızı çıktı + geri alma + geri yeşil (`git diff` boş kanıtı). Hiçbiri commit'e girmez. "Pakette ad yok" = beklenen
test adı pakette yazılmadı; kırmızıya dönen testin adını raporda yaz.

| S | Bozulan şey (paket §16) | Beklenen kırmızı |
|---|---|---|
| S-FU01-1 | tablo başlatıcısında `data-dt-data-mode`'u `server` yap | AC-1 vitest (pakette ad yok) |
| S-FU01-2 | panelin `data-dt-primary` işaretini kaldır | `diten-enter-primary.test.js` (BL-586) |
| S-FU01-3 | ürün değişim işleyicisinden SKU temizleme satırını kaldır | AC-3 testi (pakette ad yok) |
| S-FU01-4 | `rules.js`'teki 100 sınırını kaldır | AC-4 testi (pakette ad yok) |
| S-FU01-5 | önizleme gövdesi kurucusunda `lotOverride`'ı boşalt (FIX-01 öncesi `lotId: ''`; FIX-01 CT sabotajı b'nin panel karşılığı) | AC-5 vitest (pakette ad yok; FIX-01'in "item 4: the chosen lot goes into the preview" vakası FU01 DOM'uyla da kırmızı olmalı) |
| S-FU01-6 | cevap eşlemesinde satır eşini sıra yerine hep ilk satıra bağla | AC-6 testi (pakette ad yok) |
| S-FU01-7 | `rules.lotNeed`'in `RequireReason` dalını `Optional` döndür | AC-7 testi (pakette ad yok) |
| S-FU01-8 | depo değişim işleyicisinden eskitme çağrısını kaldır | AC-8 testi (pakette ad yok) |
| S-FU01-9 | gövdeye `lotOverride: null` yaz | AC-9 şema testi (pakette ad yok) |
| S-FU01-10 | `lineSeq` eşlemesini kaldır | AC-10 testi (pakette ad yok) |
| S-FU01-11 | edit modunda satır menüsünü aç | AC-11 testi (pakette ad yok) |
| S-FU01-11b | `details.js` `renderLines`'ta gerekçeyi yine rezervasyon hücresine yaz (bugünkü `:63-64`) | AC-11 gerekçe vakası (pakette ad yok) |
| S-FU01-12 | tablo hücresinde biçimleyici yerine ham değeri yaz | AC-12 testi (pakette ad yok) |
| S-FU01-13 | `ar` dosyasından bir `LinePanel.*` anahtarını sil | "resx bekçisi" (pakette ad yok; aday — çıkarım: `WarehouseS2ScreensTests.AC34_the_family_has_one_key_set_in_all_seven_languages_and_no_english_left_behind`, `frontend/Diten.Web.Tests/Warehouse/WarehouseS2ScreensTests.cs:275`) |

**Pakette adlı sabotajı olmayan DL-1 vakaları** (r3 incelemesi F-3'ün DL-1'deki karşılığı — Çekirdek 10): AC-4 klavye yolu, AC-5 "FEFO önerisi
ayrıca gösterilir", AC-8 "seçili lot sessizce sıfırlanmaz", AC-11 Edit tablosunda gerekçe satır altı metni ve Details "-", AC-12 panel
miktarı, AC-13 UAS-001 ve 375 px. Bunlara **WP ek sabotajı** önerilir (pakete aykırı değil, kanıt ekler); ad `S-WP1-a…` biçiminde, CT onayıyla
(§10 S-3). Paketin `S-FU01-n` adları değiştirilmez, yeni `S-FU01-n` uydurulmaz.

## 6. r3 incelemesinin düşük bulguları — bu WP'deki karşılığı

r3 kaydı (`2026-11-13-e7d69699.md`) dört düşük bulguyu "ilgili dilimin geliştirme paketinde" kapatılabilir der; r4 incelemesi bunların r4'te
ele alınmadığını yazar. Paket metni (r4) bunları değiştirmedi; bu WP pakete aykırı olmadan şunları yapar:

- **F-1 (Q-12 diyaloğunun vazgeç metni)** — vaka DL-3'tür (AC-29, §8.4 `:446-448`), DL-1'de uygulanmaz; DL-3 WP'sine taşınır. **DL-1'deki
  karşılığı:** satır menüsündeki "Sil" premium onayı (§8.2 `:316`, `window.showConfirm`). `_GlobalConfirmation.cshtml:325` iptal düğmesini
  her zaman çizer, `cancelButtonText` verilmezse ortak `DeleteConfirmationCancelBtn` metni (`SharedLocalizer["Cancel"]`, `:212`) gelir (`:240`). Geliştirici bu metni 7 dilde ölçer;
  onay düğmesi ("Sil") ile vazgeç metni aynı ya da geri alma gibi okunuyorsa `cancelButtonText`'i açıkça verir (FIX-01 emsali
  `details.js:187` `L().Close`) ve testi bu metni ölçer. `_GlobalConfirmation.cshtml` değişmez.
- **F-3 (adlı sabotaj eksiği)** — paketteki iki vaka (AC-29 Details "sevke hazır" bildirimi, AC-30 İlçe satırı) DL-3'tür; DL-3 WP'sine
  taşınır. DL-1'deki karşılığı yukarıdaki §5 "pakette adlı sabotajı olmayan vakalar" listesidir.
- **F-2** (UpdateOutboundShipmentHandler satır aralığı) DL-2'nin, **F-4** (Q-13 yedek yolu anahtarı) DL-3'ün işidir; bu WP'de iş yok. Not: karar
  dosyası eki (`:82-93`) Q-13'ü kapandı yazar (1.1 taslağında `status` var), F-4'ün yedek yolu bu yüzden büyük olasılıkla gereksizdir — DL-3 WP'sinde teyit.

## 7. Test koşuları

- **Dilim başı ölçümü (Adım 0 commit'inde):** tam vitest (`frontend/Diten.Web`, `npx vitest run`) geçti / kırmızı ve kırmızı adları; tam
  `frontend/Diten.Web.Tests` (`dotnet test`) geçti / kırmızı; `resx-check` (kiracı) çıktısı. FIX-01 kabul kaydındaki taban bilgi içindir:
  vitest 13 kırmızı, Web.Tests 3 kırmızı, `resx-check` FAIL (main borcu, fr / es / zh / ar / ru 35 eksik anahtar) — dilim başında yeniden ölç.
- **Web vitest — tam koşu, filtre yok:** AC-1…AC-12 testleri `frontend/Diten.Web/tests/` altında, **gerçek** `form.js` / `rules.js` /
  `details.js` dosyalarına karşı (jsdom + fetch sahtesi) (§17 `:737-742`). Mevcut bekçiler yeşil kalır: `diten-enter-primary.test.js`,
  `diten-field-icons.test.js`, `warehouse-s2-e3-fix-01.test.js` (FU01 DOM'una güncellenmiş), `global-confirm-input-type.test.js`, l10n / resx bekçileri.
- **Web birim testleri — `frontend/Diten.Web.Tests` tam koşu:** özellikle `Warehouse/WarehouseS2ScreensTests.cs` (resx ailesi 7 dil `:275`,
  sayfa yardımı 7 dil `:364`, Details çizimi) ve `Warehouse/WarehouseS2RulesNodeTests.cs`.
- **resx:** `resx-check` (kiracı, 7 dil) — DL-1 `SharedResource`'a dokunmazsa çıktı dilim başıyla bayt-eşit olmalı (§10 S-2). Sayfa resx'i
  (`OutboundShipmentsIndex.*`) `resx-check`'in kapsamında değildir; onu Web.Tests resx ailesi testi ölçer.
- **Liste ekranı protokolü:** Index dosyalarına dokunulmaz → `verify_datatable_page.py` koşmaz (§17 `:753-755`). Dokunulursa dur, CT'ye sor.
  Satır tablosu Index değildir; G-FU01-01 (`:604-608`) gerekçedir.
- **UI uyum seçicisi (CI):** `scripts/uic_changed_pages.py --base <dilim-başı>` CI'da değişen sayfaları koşar; yerelde koşturursan
  `PYTHONUTF8=1 PYTHONIOENCODING=utf-8` ve ayrık worktree (LF taban) gerekir. Paket bunu ayrıca istemez; sonucu raporda bilgi olarak yaz.
- **Build:** `Diten.Web` PASS. Servise dokunulmadığı için InventoryService paketi bu dilimde koşmaz (§17 build satırı InventoryService'i DL-2 için de sayar).
- **Canlı (CT ile birlikte):** AC-5 (iki lot, E3 verisi; panel tek satır + tablo), AC-13 (375 px `scrollWidth`, tr / en / ar), sahip turu (Enter
  panelde "Satırı kaydet"e basar, "?" metni, "tur yok") (§17 `:757`, §25 `:996-997`). Canlı turu CT yürütür; geliştirici Gateway / oturum
  açmaz, şifre girmez. Test verisi hangi outbound'larla oluştuysa raporda yazılır.
- **Ortam:** 27017 kullanma; `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşma. Tabanda kırmızı varsa aynı gün `main`'de de ölçülüp ayrılır.

## 8. Dil (localization-standard, kiracı 7 dil)

- Kullanıcıya görünen her yeni metin `OutboundShipmentsIndex.{en,tr,fr,es,zh,ar,ru}.resx`'e; JS metinleri `_IndexL10n.cshtml` /
  `index.l10n.js` köprüsünden; JS / cshtml içinde sabit metin yok. **Bir dil eksikse dilim bitmemiştir** (§11 `:615`).
- Terim: FIX-01 madde 3 ("Çıkış sevkiyatı"; hiçbir `*.tr.resx` değerinde "outbound" yok — FIX-01 bekçisi `warehouse-s2-e3-fix-01.test.js`).
- Eylem metinlerinde Title Case kuralı ve `ar` yönü (RTL) 375 px'te ölçülür. İngilizceyle aynı bırakılmış değer olmaz.

## 9. Git kuralları (GIT-002)

- Dal: `feature/sce/mod-0173-inventory-ledger-valuation`; **yeni dal açma**. `main`'e commit / push **yok**.
- Stage **yalnız açık dosya yoluyla**; `git add -A` / `git add .` **yok**; karşı oturumun (MOD-0180) dosyaları stage edilmez.
- Hook atlama (`--no-verify`) ve imza atlama yok; yıkıcı git komutu (reset --hard, checkout --, clean, stash pop, force) onaysız yok.
- Yerel commit'ler serbest; **push ve PR yok** — FU01 geliştirmesinin sonunda, ayrı kullanıcı onayıyla; push / PR'dan önce `origin/main` alınır (merge).
- Commit iletileri: `feat(sce): MOD-0178-FU01 DL-1 — …` / `test(sce): MOD-0178-FU01 DL-1 — …`.

## 10. Açık belirsizlikler — CT'ye (geliştirici kendi çözmez)

**CT kararları (2026-11-13, MVP-5 CT) — aşağıdaki sorular bu kararlarla kapandı; yeni belirsizlik çıkarsa yine CT'ye:**
- **S-1 → evet:** `PageHelpResource.{7}.resx` içinde yalnız `Help.OUTBOUNDSHIPMENTS.*` anahtarları ve `PageHelpCatalog.cs`'te yalnız
  OUTBOUNDSHIPMENTS girdisi düzenlenebilir (MOD-0178'in kendi yardımı; eklemeli ya da yalnız bu anahtarların değeri). Başka sayfanın
  anahtarına dokunma; 7 dil.
- **S-2 → ölçüt:** AC-13 resx = (i) sayfa resx ailesi 7 dilde tam (`WarehouseS2ScreensTests` resx ailesi testi yeşil) ve (ii) `resx-check`
  çıktısı dilim başıyla karşılaştırıldığında **yeni bulgu yok** (main borcu olan 35 eksik anahtar sayılmaz; fark raporda).
- **S-3 → kabul:** `S-WP1-*` ek sabotajları AC-4 (klavye), AC-8 (lot sıfırlanmaz), AC-11 (Edit gerekçe satır altı ve Details "-"), AC-12
  (panel) ve AC-13 (UAS-001 / 375 px) için koşulur; her biri kırmızı → geri yeşil, kanıtlı.
- **S-4 → (b):** Kaydet gövdesini servise giden biçimde üreten üretim kodu Web proxy'sidir (`OutboundShipmentsController.cs`, `lotOverride`
  kurulumu `:428` civarı). AC-9 şema denetimi `frontend/Diten.Web.Tests` altında xUnit testi olarak yazılır: proxy'nin ürettiği gövde,
  dondurulmuş `docs/analysis/contracts/wms-bundle.openapi.yaml` `CreateOutboundShipmentRequest` şemasına (YamlDotNet — projede zaten var)
  karşı doğrulanır (`additionalProperties: false`, `lotOverride` yalnız lotlu satır, `null` yok). Yeni npm bağımlılığı **eklenmez**.
  Tarayıcı tarafı satır sırası vitest'te ölçülür. Paket §17 `:738`'deki "vitest" ifadesinden sapma CT kararıdır, kabul kaydına yazılır.
- **S-5 → Adım 0 madde 2-3** yukarıda güncellendi.
- **S-6 → evet, koşullu:** `WarehouseS2ScreensTests.cs` aynı modülün (MOD-0178) testidir, XMC değildir; yalnız FU01'in değiştirdiği DOM'a
  bağlı doğrulamalar yeni DOM'a göre güncellenir, zayıflatma yok, her değişiklik raporda gerekçeli.
- **S-7 → ölçüldü:** servis okuması satır düzeyinde `lotOverride.fefoBypassReason` döndürür
  (`services/Diten.InventoryService/src/Diten.InventoryService.Application/Features/OutboundShipments/Services/OutboundShipmentReads.cs:85`,
  `OutboundLotOverrideDto`). Edit tablosu gerekçeyi buradan okur. Proxy bu alanı Web'e geçirmiyorsa yalnız Web proxy / model düzeltmesi
  DL-1 kapsamındadır; servis değişikliği gerekirse **dur**, CT'ye sor.
- **r3 F-1 / F-3 → DL-3'e taşındı** (AC-29, AC-30); DL-1'deki karşılıkları (satır "Sil" onayında vazgeç metni) bu WP'de kalır.


- **S-1 Sayfa yardımı dosyası:** `Help.OUTBOUNDSHIPMENTS.*` sayfa resx'inde değil, ortak `frontend/Diten.Web/Resources/PageHelpResource.{7}.resx`
  (`PageHelpResource.tr.resx:2125-2167`) içinde; adım ikonları `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs:95`. Paket §5 DL-1 listesi
  bu dosyaları saymaz, ama AC-13 ve §25 yardımın güncellenmesini ister. Geliştirici bu dosyalara dokunmadan önce CT onayı alır (ortak dosya).
- **S-2 AC-13 "resx-check temiz":** `resx-check` yalnız `SharedResource`'u denetler ve main borcuyla FAIL'dir (FIX-01 kabul kaydı). Ölçüt
  "dilim başıyla bayt-eşit + sayfa resx ailesi 7 dilde tam" olarak mı okunacak?
- **S-3 Ek sabotajlar:** §5'teki `S-WP1-*` önerileri kabul mü; kabulse hangi vakalar?
- **S-4 AC-9 şema testi:** paket vitest'in dondurulmuş `wms-bundle.openapi.yaml`'ı okumasını ister (§17 `:738`); `frontend/Diten.Web/package.json`'da
  YAML ayrıştırıcısı yok ve bugün hiçbir vitest openapi dosyası okumuyor. Yeni bağımlılık eklemek `configuration-safety` işidir. Seçenekler: (a)
  bağımlılıksız dar okuyucu, (b) şemayı `frontend/Diten.Web.Tests` xUnit testinde doğrulamak, (c) devDependency. Karar CT'nin.
- **S-5 Adım 0 ve ikinci oturum:** aynı checkout'ta MOD-0180 oturumunun commit'lenmemiş işi varsa Adım 0 kuralı gereği durulur; ff-merge
  ortak dalın ucunu iki oturum için birden taşır. Sıralamayı CT belirler.
- **S-6 Ebeveyn testleri:** `WarehouseS2ScreensTests.cs` eski kart DOM'una bağlı bir doğrulama içerirse güncellenmesi paket §5 DL-1 listesinde
  açıkça yok (yalnız `frontend/Diten.Web/tests/**`). Aynı modülün testi olduğu için güncellenir mi, yoksa CT onayı mı ister?
- **S-7 Edit tablosunun gerekçe verisi:** Edit `GET /Warehouse/OutboundShipments/outbound/{id}` okumasını kullanır (`form.js:276`); cevapta
  tahsislerin `fefoBypassReason`'ı yoksa (ölçülecek) AC-11'in Edit vakası servis / proxy değişikliği gerektirir → dur, CT'ye sor (DL-1 yalnız Web).

## 11. Teslim raporu biçimi

1. Adım 0: `git status` çıktısı, ff-merge sonucu ve dilim başı SHA.
2. Commit listesi (SHA + ileti) ve her commit'in dosya listesi (yalnız §3 yolları; karşı oturum dosyası yok kanıtı).
3. Test sayıları: vitest ve `Diten.Web.Tests` (önce → sonra; geçti / kırmızı / atlandı, kırmızılar adıyla, tabanla karşılaştırma), `resx-check` önce / sonra.
4. AC-1…AC-13: her biri için test dosyası + test adı (ya da canlı kanıt).
5. Her sabotaj (§5, 14 kalem + kabul edilen `S-WP1-*`): değişiklik, kırmızı test adı + çıktı özeti, geri alma, geri yeşil, `git diff` boş.
6. `warehouse-s2-e3-fix-01.test.js` ve (varsa) `WarehouseS2ScreensTests.cs` farkının gerekçesi — hiçbir doğrulama zayıflamadı kanıtı.
7. Yeni resx anahtarları listesi (7 dil) ve sayfa yardımı farkı.
8. `details.js` farkının yalnız `renderLines`'ta olduğu kanıtı (`:168-245` onay akışları bayt-eşit).
9. Canlı tur için CT'ye hazır notu: ekran görüntüsü listesi (tr / en / ar, 375 px) ve kullanılan test verisi.
10. Açık kalanlar ve CT'ye sorular (§10'un cevaplanmayanları dahil).

## 12. Kural haritası (AGENTS.md §6.1)

- **Daima:** `multi-tenancy`, `security-jwt`, `git-safety` (GIT-002), `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001).
- **Ekran / sayfa:** `frontend-standards`, `views-organization`, `unauthorized-surface-standard` (UAS-001), `frontend-js-standard`,
  `premium-modal-standard` (MOD-0013 — satır silme onayı), `frontend-datatable-template` (DataTable v2, Golden Compact / Slim, istemci modu),
  `frontend-details-template` (Details tahsis tablosu), `frontend-form-template` (Compact `_Form`, bağımlı select2 kuralı),
  `page-help-and-keyboard` (UXH-001: "?" metni, yan panelde Enter, tur yok).
- **Kullanıcıya görünen metin:** `localization-standard` (kiracı 7 dil), `dynamic-localization-standard`.
- **Ortam / kanıt:** `dev-runbook`, `configuration-safety` (S-4), `status-reporting-and-evidence` (REP-001).

Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.
