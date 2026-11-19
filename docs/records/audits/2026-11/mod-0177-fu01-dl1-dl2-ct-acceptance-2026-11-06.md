# MOD-0177-FU01 DL-1 + DL-2 — Control Tower kabulü

- Tarih: 2026-11-06. CT: MVP-5 CT (Beste, merkez), oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `a872e8c56`
  (DL-1 + DL-2 birlikte; DL-1 tek başına kullanıcıya açılmaz, commit iletisi).
- İncelenen: `f57172eed` (feat(inventory): MOD-0177-FU01 DL-1 — recall customer identity from CRM (backend, RECALL 1.10.0)) ve `a872e8c56`
  (feat(web): MOD-0177-FU01 DL-2 — recall screens show the CRM customer by name, never its key); taban `2005dc238` (MOD-0178 DL-5).
  MVP-1 hattında (`feature/sce/mod-0173-inventory-ledger-valuation`); push / PR yok.
- Paket: `MOD-0177-FU01-recall-crm-customer-ref.md` r8 @ sha256 `fc7f3cd39e46df0471887f637aa0ea2d1321a8cdd67479cda93b7c19f66bdef1`, `f57172eed` ve
  `a872e8c56`'da yeniden ölçüldü (`git show` baytları, Python); onay satırı `execution/approvals/approvals.tsv` (MOD-0177-FU01, HIGH, `bd52f34fc`).
- Sonuç: **VERIFIED** — kod ve testler ölçüldü, sayılar geliştiriciyle birebir, iki istenen CT sabotajı kırmızı. Bir DÜŞÜK bulgu (F-1, S-101:
  sürüm değişmez). ACCEPTED dört kapıya bağlı: D-1 (SHIPMENT-BUNDLE 3.5.0, AC-17), D-4 (rol ataması, Ali), AC-20 (f) veri sayımı, AC-30.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam DL-1 (`2005dc238..f57172eed`) | 58 dosya: Inventory `src` 33 (yalnız `Recall/` klasörleri), `tests` 14 (`Recall/` 13 + `Contract/Fixtures/recall-v1.9.0.openapi.yaml`), `docs/analysis/contracts/recall.openapi.yaml`, Web `Resources/Views/Recall/Recall.*.resx` 7 + `Views/Recall/Recall.cs`, `Diten.Web.Tests/Recall/EvidenceTextParityTests.cs`, `RecallWebTests.cs` |
| Kapsam DL-2 (`f57172eed..a872e8c56`) | 13 dosya: `case-detail.js`, `trace-view.js`, `trace.js`, Web `RecallController.cs`, `PageHelpResource.*.resx` 7 (yalnız `Help.RECALLCASEDETAILS.Step4`), `recall-fu01.test.mjs`, `RecallFu01WebTests.cs` — hepsi §5 DL-2 listesinde |
| Korunan yollar / XMC | `ocelot.json`, `.antigravity/**`, `_Layout.cshtml`, CRM / SupplyChain / Auth servisi, `shipment-bundle.openapi.yaml`, MOD-0173…0176 dosyası **yok**; `RecallContract120…180Tests` değişmedi. XMC gerektiren dosya yok |
| §5 dışı (adı listede olmayan) dosyalar | `Domain/Recall/Enums/RecallEnums.cs` (+13, yalnız yeni `RecallCustomerRefSource` enum'u), `Services/RecallCustomerMatch.cs` (yeni, 42 satır), `Evidence/RecallEvidenceText.cs` (+24, `CustomerCell`), `Diten.Web.Tests/Recall/RecallWebTests.cs` (+2 ad: "{0} · {1}" biçimli iki anahtar İngilizceyle aynı kalabilir — AC-19'un istisnası). Hepsi Recall modülü, eklemeli → F-2 DÜŞÜK |
| Anahtar sırası | `RecallTraceEngine.ShipmentsAsync`: `customerAccountId` (Crm) → `shipToReference` (yalnız `"D"` biçimli değilse) → `DOC:` |
| AC-17 Skip | `RecallCrmLoggingTests.cs:64` `ShipmentBundle350AlignmentTests.CustomerRef_field_matches` — `[Fact(Skip = "MOD-0177-FU01 D-1: SHIPMENT-BUNDLE 3.5.0 not frozen")]`, metin paketin AC-17 satırıyla birebir; sınıf özeti D-1'e bağlı ve "Skip kaldırılmadan DL-1 kabul edilmez" der. Tam koşuda atlanmış görünür (kırmızı değil) |
| Tam Inventory takımı (filtresiz; `INVENTORY_TEST_MONGO` yok, `DITEN_TEST_MONGOD` = mongod 9.0, 27017 yok) | **1898 / 1903, 5 atlandı, 0 kırmızı** — geliştiriciyle aynı. Atlananlar: AC-17 (D-1) + 4 performans testi (tabanda da var; DL-5'te 1844 / 1848, 4 atlandı) |
| Tam Web takımı | **4257 / 4261, 4 kırmızı** — `CapacityPlansPageTests` ve `SandopPlansPageTests` `A_failed_or_denied_first_load_leaves_no_loading_title`, `NavManifestL10nGuardTests` ×2; geliştiricinin bildirdiği taban kırmızılarıyla isim isim aynı, Recall dışı |
| TenantArchitecture | **221 / 227, 6 kırmızı** — `DocsPathGuardTests` ×5 + `AuditTrailStandardTests.EveryWriteCommand_IsAudited_…`; geliştiriciyle aynı. AuditTrail iletisindeki 19 komut (Visit / Planning / Doctor / Shelf) Recall'a ait değil → FU01 yeni denetimsiz komut 0 |
| Geliştirici ölçümleri (CT tekrar koşmadı) | 100 + 23 sabotaj kırmızı (DL-1 + DL-2) |

## CT sabotajları (uygulandı → sonuç → geri alındı)

Her biri tek satır → derleme → `Recall` ad alanı (Inventory 285 test / Web 320 test) → `git checkout` → sha256 eşit; iş sonunda ağaç temiz.

| # | Sabotaj | Sonuç |
|---|---|---|
| CT-1 | `RecallTraceEngine.ShipmentsAsync` (sha256 `da94183b…21d0`): Crm dalına `&& read.ShipToReference is not { Length: > 0 }` eklendi — `shipToReference` varken `customerRef`'in önüne geçer | kırmızı: 29 test, aralarında `RecallCrmCustomerKeyTests.CustomerRef_wins_over_shipTo`, `Case_insensitive_key`, `RecallCrmReconcileTests` ×7, `RecallCrmNoGuidDisplayTests` ×3, `RecallCrmEvidenceTests` ×2 (253 / 282) |
| CT-2 | aynı dosya: `shipToReference` dalından `&& !RecallCustomerKeyRules.IsDShaped(shipTo)` silindi (`"D"` biçimli ship-to CRM anahtarı gibi kullanılır) | kırmızı: `RecallCrmCustomerKeyTests.Fallback_order_shipTo_then_DOC_for_every_unusable_customerRef` (281 / 282) |
| CT-3 | `case-detail.js` (sha256 `f3328472…6249`) müşteriler sekmesi: CRM satırının adı `cv.text(r.customer, t, r.label)` yerine `r.customerRef` (GUID) basıldı | kırmızı: `RecallFu01NodeTests.The_customer_scripts_hold_against_the_shipped_files` (`recall-fu01.test.mjs` "customers tab never prints the account id"; 319 / 320) |
| CT-4 | `case-detail.js` `RecallCustomerView.text` son satırı: `GUID.test(c.label) ? '' : c.label` → `c.label` (Legacy / NoCrm satırında GUID etiket süzgeci kalkar) | **YEŞİL — 320 / 320** (F-1) |

## Bulgular

- **F-1 (DÜŞÜK, S-101):** Legacy / NoCrm satırının etiketi serviste `label ?? facts.Key`'dir (`RecallCustomerDisplay.cs:106`, `:108`;
  `RecallReads.cs:209` anahtarı etiket verir). FU01 öncesi bir anahtar `"D"` biçimliyse ekranda GUID'i yalnız Web'deki `GUID` süzgeci saklar;
  bu süzgeç testsiz (CT-4 yeşil). Paket bu verinin gerçekte olmadığını AC-20 (f) sayımıyla kanıtlatır (sıfır değilse DL-1 kabulü durur);
  bu yüzden DÜŞÜK. Düzeltme: `recall-fu01.test.mjs`'e `"D"` biçimli etiketli Legacy satırı vakası (CT-4 kırmızı olmalı) — AC-20 (f)
  sonucu 0 değilse önce CT kararı.
- **F-2 (DÜŞÜK, S-101):** §5 Repo Scope'ta adı geçmeyen dört dosya (yukarıdaki tablo). Hepsi Recall modülünde, eklemeli ve kapsamdaki işin
  yardımcısı; paketin bir sonraki revizyonunda §5'e yazılması önerilir. Sürüm değişmez.

## Açık kalanlar (ACCEPTED kapıları)

- **D-1 / AC-17:** SHIPMENT-BUNDLE 3.5.0 FROZEN (MVP-6; bugün 3.4.0), `customerRef` alanı teyitli ve `shipToReference`'ın `"D"` GUID
  olmayacağı yazılı. Kabul commit'i Skip'i kaldırır, test yeşil olur, S-17 sabotajı koşulur; kabul kaydı o commit'i yazar.
- **D-4:** `crm.account.lookup-validation` izninin geri çağırma rollerine atanması — Auth WP'si (Ali), Q-4 yanıtı.
- **AC-20 (f):** kiracı başına `"D"` biçimli eski anahtar sayımı (beklenen 0; F-1 bununla bağlı). Geçiş kaydında imzalı.
- **AC-30:** canlı geçiş öncesi rol kontrolü (kayıt ve dışa aktarım rollerinde eksik atama 0), kabul ortamında D-4 ile.
- E3 (canlı): DL-2 ekranları Gateway 5000 üzerinden oturumdaki kullanıcıyla — D-1 ve D-4 kapanınca.

ACCEPTED, kod main'e girdiğinde ve dört kapı kapandığında yazılır (SOP §7).
