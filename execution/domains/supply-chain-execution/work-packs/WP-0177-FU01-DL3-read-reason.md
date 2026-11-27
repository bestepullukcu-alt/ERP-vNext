# WP-0177-FU01-DL3 — Geri çağırmada CRM müşteri kimliği DL-3: sevkiyat okuma nedeni (`CustomerSourceReason`, RECALL 1.11.0, üç metin)

| Alan | Değer |
|---|---|
| Durum | **draft** — CT başlama mesajı (etiket adı) gelmeden çalıştırılmaz. Bu WP push, PR ya da `.antigravity/**` değişikliği izni değildir |
| Sahip | Beste (merkez; MVP-1 CT). Geliştirici: merkez geliştirici oturumu. CT doğrulayıcı: MVP-1 CT |
| Tarih | 2026-11-27 · Sahip kararı: "MOD-0177-FU01 DL-3 — devam edelim" (Beste, `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3) |
| Modül | **MOD-0177-FU01** Recall customer identity from CRM (ebeveyn MOD-0177; supply-chain-execution; `Diten.InventoryService` 5067, Recall süreç içi + `frontend/Diten.Web`; kiracı modülü, 7 dil) |
| Kimlik ön kontrolü | `python -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0177-FU01 --name "Recall customer identity from CRM" --parent MOD-0177` → `OK … proven against Blueprint/registry`, çıkış 0 (2026-11-27, worktree `wms-ct-01`) |
| Paket | `execution/domains/supply-chain-execution/module-packs/MOD-0177-FU01-recall-crm-customer-ref.md` **r10**, LF sha256 `6181b276e50e5aa3895b6c649870041ca08d04d1a8371e5cf1a217c7f3166553` (`MSYS_NO_PATHCONV=1 git show origin/main:<yol> \| sha256sum`, 2026-11-27) |
| Onay | `execution/approvals/approvals.tsv:115` (origin/main `85e3b1eec`, PR #462) — onaylayan Ali (GMG-ERP) «onaylıyorum» 2026-11-25, yazan `bestepullukcu-alt`; not: "Beste K-1…K-5 ve K-7 «uygun» (2026-11-24); bağımsız inceleme r10 UYGUN". `python scripts/pack_approval.py check --ref origin/main <paket>` → **onaylı — HIGH · GMG-ERP · 2026-11-25, çıkış 0** (2026-11-27) |
| İnceleme | `docs/records/reviews/MOD-0177-FU01/2026-11-23-6181b276.md` — UYGUN (F-1 ORTA, F-2 / F-3 DÜŞÜK, üçü de r10 içinde kapandı; iki bilgi notu → bu WP §5.4) |
| Dilim | **DL-3** (paket §22 DL-3 satırı). Kanıt seviyesi **E4** |
| Yetki sırası | paket > `execution/domains/supply-chain-execution/domain-config.md` > `AGENTS.md` > `.antigravity/`. WP ile paket çelişirse paket geçerlidir; çelişkiyi CT'ye yaz, kendin çözme |

## 0. Başlama kapıları

| # | Kapı | Durum (ölçüldü 2026-11-27) |
|---|---|---|
| G-1 | r10 CT-APR-02 bağımsız inceleme + CT-APR-01 onay satırı DL-3 kodundan önce (§22 kapı (1)) | **kapalı** — inceleme UYGUN, onay satırı main'de (`approvals.tsv:115`), `pack_approval.py check` çıkış 0 |
| G-2 | D-7: DL-1 + DL-2 kodu hatta (§7 D-7, §22 önkoşul) | **kapalı** — DL-1 `f57172eed`, DL-2 `a872e8c56` (main'de, PR #424); hat dalı `feature/sce/mod-0173-inventory-ledger-valuation` (`8c32f4d90`) ikisini de taşır; canlı `recall.openapi.yaml` hatta ve main'de `version: 1.10.0`, LF sha256 `21e76b77…5f4f` = `f57172eed` blob'u (paketin dondurulacak 1.10.0'ı) |
| G-3 | K-1…K-5 sahip yanıtı (§22 kapı (5)) | **kapalı** — Beste "uygun" (2026-11-24; onay satırı notu): yazar önerileri aynen geçerli. K-6 inceleme F-1 ile kapandı. K-7: bu WP'den sonra DL-1 / DL-2 kabul kayıtları r10'u anar |
| G-4 | DL-3 D-1…D-5'e (SHIPMENT 3.5.0, CRM, Auth) bağlı değil (§22) | bilgi — bekletmez |
| G-5 | **S-1 (açık, sözleşme commit'inden önce):** §8.11 madde 5'in açıklama metni ile F-1 kuralı arasındaki fark (§5.3) | **açık** — sözleşme commit'i S-1 cevabından önce yapılmaz; arka uç ve ekran işi beklemez ama sıra "önce sözleşme" olduğu için pratikte dilimi bekletir |
| G-6 | CT başlama mesajı: ff etiketi (`ct/line-ff-<tarih><harf>`) | **açık** — CT verir |

**Sonuç:** başlama kapısı **kapalı değil, iki açık madde var** (G-5 S-1 cevabı, G-6 CT etiketi). İkisi gelince DL-3 başlar.

## 1. Adım 0 — dal ve ölçümler

1. Ana checkout'ta (`C:\Users\Ali\erp-vnext`) tek çalışma dalı **`feature/sce/mod-0173-inventory-ledger-valuation`**. Yeni dal açılmaz.
2. CT etiketine ileri sar: `git merge --ff-only <CT etiketi>`. ff olmazsa `git merge --no-ff <CT etiketi> -m "merge: CT chain <etiket> into MVP-1 line (CT standing approval, docs/records only)"`. Çakışmada `git merge --abort`, dur, CT'ye yaz. Etiket yoksa dur. Rebase yok.
3. Paketin LF sha256'sı hat dalında ölçülür: `MSYS_NO_PATHCONV=1 git show HEAD:execution/domains/supply-chain-execution/module-packs/MOD-0177-FU01-recall-crm-customer-ref.md | sha256sum` → `6181b276` ile başlamalı; değilse dur.
4. `MSYS_NO_PATHCONV=1 git show HEAD:docs/analysis/contracts/recall.openapi.yaml | sha256sum` → `21e76b77f30d130ab2d103004df32c462ceec47cf07935a636b8a7c928eb5f4f`; değilse dur (1.10.0 fixture'ı bu blob'dan alınır).
5. **Eşzamanlılık:** ana checkout'ta derleme / test / sabotaj **yok** (Inventory 5067 orada `dotnet watch` ile koşuyor, fleet kilidi). Derleme, test ve sabotaj ayrık worktree'de:
   `git -C C:\Users\Ali\erp-vnext -c core.autocrlf=false worktree add --detach C:\Users\Ali\erp-vnext\.claude\worktrees\verify-0177-dl3 <SHA>` (LF; fixture ve sözleşme bayt ölçümü CRLF'den etkilenmesin).
   Taban için ikinci ayrık worktree `…\verify-0177-dl3-base` etiket üzerinde. Commit'ten hemen önce hattı yeniden al; `Recall.*.resx`, `Recall.cs`, `docs/analysis/contracts/README.md` başka işte de değiştiyse iki tarafın satırlarını koru ve CT'ye yaz.
   Not (ölçüldü): hat dalında `README.md` ile CT zinciri arasında bir satır farkı var (TENANT-SECRET-VAULT satırı CT zincirinde, hatta yok) — alımda README çakışırsa iki satır da korunur.
6. Dilim başı ölçümleri (§7) bu noktada alınır ve teslim raporuna yazılır.

## 2. Kapsam — paketten birebir

**Paket §22, DL-3 satırı (alıntı):**

> | DL-3 | okuma nedeni (r10): `CustomerSourceReason` yazımı (açılış, yeniden izleme, revizyon) + RECALL 1.11.0 + vaka ayrıntısı ve iz ekranında üç metin (7 dil) | Owned Objects, Entity Fields, Repo Scope, Dependencies, Durum ve komut tablosu, Okuma nedeni ve RECALL 1.11.0, Frontend File Contract, Failure Path to Verify, Audited Events | E4 | plan satırı yok | MVP-1 |

**Paket §22, "DL-3 (r10) — kabul ölçütleri ve kapısı" (alıntı):**

> Kabul ölçütleri: AC-32…AC-41. Dilim numarası pakette sıradaki boş numaradır (DL-1, DL-2 kullanıldı). Önkoşul: DL-1 ve DL-2 kodu hatta (D-7); DL-3 D-1…D-5'e bağlı değildir. **Dilim içi sıra:** önce sözleşme (RECALL 1.11.0, `recall-v1.10.0` fixture'ı, `RecallContract1110Tests`, `RecallContract1100Tests` "after" çevrimi, `RecallRulesAndShapeTests` sürüm satırı) — `ScopeItem` kapalı nesne olduğundan neden taşıyan yanıt ve Web sahteleri 1.11.0'sız kırmızıdır (§6 r10 notu) —, sonra arka uç, sonra ekran. **Kabul kapısı:** (1) r10 CT-APR-02 bağımsız inceleme ve CT-APR-01 onay satırı DL-3 kodundan önce (künye `ready-for-dev` bu kapıyı kapatmaz); (2) AC-32…AC-41 yeşil ve her sabotaj üretim kodunda tek tek "kırmızı → geri al → yeşil, diff boş" (kabul kaydına); (3) tam paket: filtresiz Inventory test paketi + `Diten.Web.Tests` (Node testleri dahil), temel kırmızılar aynı gün `main` ile karşılaştırılır, yeni kırmızı 0; (4) AC-37 fark testi ve AC-38 dondurulmuş vektörler — mevcut vektör testleri değiştirilmeden — yeşil; (5) K-1…K-5'in sahip / CT yanıtı ya da yazar önerisinin kabulü kabul kaydında (K-6 kapandı); (6) UIC seçici ve tarayıcı kontrolü (tr / en, 375 px) vaka ayrıntısında ve iz ekranında; (7) `docs/analysis/contracts/README.md` sözleşme kaydında RECALL-BUNDLE 1.10.0 ve 1.11.0 satırları var (inceleme F-3, §5).
> **Açılış:** DL-3 DL-1 + DL-2 ile birlikte ya da onlardan sonra açılır; tek başına açılamaz (DL-1 kodunun üzerindedir). DL-3 olmadan DL-1 + DL-2 açılabilir (alan yoktur, metin çizilmez — eski vaka davranışı, AC-35 / AC-40). Kapanış smoke'u §17 (E5).

Kural kaynağı: §8.11 (tek kaynak `RecallTraceService.ScopeItems`, yeniden izleme tablosu, revizyon, okuma, mühür / kanıt / denetim dışılığı, sözleşme farkının beş maddesi, gösterim). Bu WP §8.11'i özetlemez — geliştirici §8.11'i satır satır uygular.

**Kapsam DIŞI (paket):** yeniden deneme / bütçe ve vakayı reddetme (O-9); `CustomerDisplay`, `TraceNode`, `CaseCustomerShipment` şemaları; yeni uç, kod, izin, parametre; rs-scope-v1, rt-v1, kanıt paketi, iz CSV'si ve denetim ayrıntısında neden; kapsam farkı, etkinlik, iade bağları, pencereler ve sayfa yardımı metni (K-4); D-1…D-5 (SHIPMENT 3.5.0, CRM, Auth).

## 3. Dokunulacak dosyalar (paket §5 "DL-3" listesi; satır numaraları `b3c89f435` — hat dalında bu dosyalar o tabanla aynı, ölçüldü)

Kısaltmalar: `INV/` = `services/Diten.InventoryService/src/Diten.InventoryService.`; `INVT/` = `services/Diten.InventoryService/tests/Diten.InventoryService.Tests/`.

| Commit | Yol | Ne (yalnız bu) |
|---|---|---|
| C1 sözleşme | `docs/analysis/contracts/recall.openapi.yaml` | 1.10.0 → 1.11.0, **tam olarak** §8.11'in beş maddesi (sürüm, değişiklik notu ilk satırı, `CustomerSourceReason` şeması, `ScopeItem` ve `CustomerRow` üyeleri). Madde 5'in açıklama metni S-1'e bağlı (§5.3) |
| C1 | `docs/analysis/contracts/README.md` | yalnız sözleşme kaydı tablosu: 1.9.0 satırının ardına **RECALL-BUNDLE 1.10.0** ve **RECALL-BUNDLE 1.11.0** satırları (§5 metni); öteki satırlar değişmez (inceleme F-3, kapı (7)) |
| C1 | `INVT/Contract/Fixtures/recall-v1.10.0.openapi.yaml` (yeni) | `f57172eed:docs/analysis/contracts/recall.openapi.yaml` blob'u bayt eşit (LF sha256 `21e76b77…5f4f`) |
| C1 | `INVT/Recall/RecallContract1110Tests.cs` (yeni) | AC-37 fark testi (`RecallContract1100Tests.cs` `JsonNode.DeepEquals` deseni) |
| C1 | `INVT/Recall/RecallContract1100Tests.cs` | yalnız "after" satırı (bugün `:52` `var after = Load(ContractSchemas.RecallContractPath);`) → 1.10.0 fixture'ı (emsal `RecallContract190Tests.cs:40`) |
| C1 | `INVT/Recall/RecallRulesAndShapeTests.cs` | yalnız `:196` sürüm satırı `1.11.0` ve 1.11.0 belirteçleri (`:224-226` deseni) |
| C2 arka uç | `INV/Domain/Recall/Entities/RecallEntities.cs` | yalnız `RecallScopeItem`'a `CustomerSourceReason` (`ShipmentReadOutcome?`), `NoCrmWarning`'den (`:299`) sonra |
| C2 | `INV/Application/Features/Recall/Trace/RecallTraceService.cs` | yalnız `ScopeItems` (`:82`; `:115` `NoCrmWarning` satırının yanında) ve `Reconcile` (`:133`) / `CustomerKey` (`:198`) — §8.11 tabloları |
| C2 | `INV/Application/Features/Recall/Handlers/R3Handlers.cs` | yalnız `ReviseRecallScopeHandler` kopyası `:555-563` (`CustomerSourceReason = i.CustomerSourceReason`); delta `:530` değişmez |
| C2 | `INV/Application/Features/Recall/Handlers/CaseDetailReadHandlers.cs` | yalnız `GetRecallCaseCustomersHandler` satır kurulumu `:100` — "en son yazılan kalem" kuralı (§8.11, F-1) |
| C2 | `INV/Application/Features/Recall/Services/RecallReads.cs` | yalnız `Item` `:216` — kalemin nedeni |
| C2 | `INV/Application/Features/Recall/RecallModels.cs` | yalnız `RecallScopeItemDto` `:177` ve `RecallCustomerRowDto` `:251` sonuna `string? CustomerSourceReason = null` (`null` yazılmaz — Api `Program.cs:111` `WhenWritingNull`) |
| C2 | `INVT/Recall/RecallCustomerSourceReasonTests.cs` (yeni) | AC-32…AC-36, AC-38; MVP-6 ikizi `INVT/Recall/ShipmentReaderTests.cs` `ShipmentDoubleBehaviour` belge başına davranışla — ikize ek gerekirse **yalnız test altyapısında** ve raporda adıyla |
| C3 ekran | `frontend/Diten.Web/wwwroot/assets/js/Recall/case-detail.js` | yalnız saf `RecallCustomerView` parçası (`:15-65`) — neden metni işlevi ve dışa açık listeye (`:65`) adı; kapsam sekmesi yer hücresi `:294`; müşteriler sekmesi müşteri hücresi `:434` |
| C3 | `frontend/Diten.Web/wwwroot/assets/js/Recall/trace-view.js` | yalnız `describe` Shipment dalı `:65` (K-2) |
| C3 | `frontend/Diten.Web/Resources/Views/Recall/Recall.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Views/Recall/Recall.cs` | üç anahtar `Recall.Customer.SourceReason.{Unavailable,Forbidden,NotFound}` (§11); tr = sahip metni harfiyen; fr / es / zh / ar / ru gerçek çeviri |
| C3 | `frontend/Diten.Web.Tests/JavaScript/Node/recall-fu01-reason.test.mjs` (yeni), `frontend/Diten.Web.Tests/Recall/RecallFu01ReasonWebTests.cs` (yeni) | AC-39…AC-41 (`recall-fu01.test.mjs` sahte DOM deseni; `RecallFu01NodeTests`'in `# pass 9` sabitlemesi — `RecallFu01WebTests.cs:32` — değişmesin diye ayrı dosya) |

**Değişmez (paket §5 DL-3, aynen):** `CaseCommandHandlers.cs` (`:111`, `:428`, `:560`), `RecallTraceEngine.cs`, `TraceModel.cs`, `TraceRules.cs`, `RecallRules.cs` (`RecallScopeRules.Canonical`), `RecallEvidence.cs` ve `RecallEvidenceText_*.resx`, `RecallCustomerDisplay.cs`, `ShipmentBundleHttpReader.cs`, `RecallTraceService.Csv`, `RecallSignableSubjects.cs`, denetim yazıcıları, izinler, Gateway; `recall.l10n.js`; teslim sütunu (`case-detail.js:404-407`), `Recall.Read.*`, `Recall.Shipment.Unreadable`, `PageHelpResource.*.resx`; `recall-fu01.test.mjs`, `recall-r6.test.mjs`, `EvidenceTextParityTests.cs`; `RecallContract120Tests` … `RecallContract190Tests`, `RecallESignTests`, Web `RecallContractFakesTests`, `RecallTenantBoundaryTests`.

## 4. Dokunulmayacaklar ve XMC-001

**Korunan yollar (§6, AGENTS.md §4):** `.antigravity/**`, `gateway/Diten.ApiGateway/ocelot.json`, `_Layout.cshtml`, Archive; `services/Diten.CrmService/**`, `services/Diten.SupplyChainService/**`, Auth; `docs/analysis/contracts/shipment-bundle.openapi.yaml`, `drafts/crm-account-lookup-validation-v1.0-proposal.md`; MOD-0173 / 0174 / 0175 / 0176 dosyaları; paket dosyası ve karar kayıtları.

| # | Dosya | Değişiklik | Sahip | Onay kaydı | Not |
|---|---|---|---|---|---|
| X-1 | `docs/analysis/contracts/README.md` (ortak sözleşme kaydı) | yalnız iki RECALL satırı, ekleme | sözleşme kaydı ortak; satırlar MOD-0177'nin | paket §5 DL-3 + inceleme F-3 (onaylı r10) | başka satır değişmez |
| X-2 | `INVT/Recall/ShipmentReaderTests.cs` (`ShipmentDoubleBehaviour`) | yalnız gerekiyorsa eklemeli ikiz davranışı | MOD-0177 (Recall test altyapısı) | paket §5 DL-3 "Testler" | XMC gerekmez; raporda adıyla |
| X-3 | `INV/Api/Recall/RecallServiceCollectionExtensions.cs` | **yalnız S-36 sabotajı** (geçici, bayt eşit geri) | MOD-0177 | — | değişiklik değil, sabotaj |

Bu tabloda olmayan başka modül dosyasına dokunmak gerekirse **dur**, XMC-001 ile CT'ye sor. Servis tarafında DL-1 / DL-2 davranışında boşluk görülürse dokunma, CT'ye yaz.

## 5. Kurallar, kararlar ve açık noktalar

### 5.1 Bağlayıcı kurallar (paket)
- **Kiracı:** başka kiracının vakası 404 (bugünkü); MVP-6 okuması çağıranın token'ıyla (`ForwardCallerHeadersHandler`); sabit / varsayılan kiracı yok (AC-36).
- **Mühür / kanıt / denetim:** `CustomerSourceReason` rs-scope-v1, rt-v1, kanıt paketi, `exportTrace` CSV ve denetim ayrıntısına **girmez**; dondurulmuş vektörler bayt eşit (AC-38). Yeni yazma komutu ve yeni denetim olayı yok; `tests/architecture/audit-ledger/Diten.InventoryService.md` değişmez (§21 r10).
- **Okuma:** neden kalıcıdır, canlı okumadan **türetilmez**; okuma yolu belgeyi yeniden yazmaz (AC-35, AC-40).
- **Dil:** kiracı modülü 7 dil; hardcoded metin yok (AC-41, S-41d). `zh` / `ar` / `ru` İngilizce kopya olamaz.
- **UAS-001 / izin:** yeni eylem, yeni izin, yeni sayfa yok; mevcut `[HasPermission]` ve görünüm kapısı değişmez.
- **Golden / BL-586:** yeni sayfa / modal / form alanı yok (`form_field_count: 0`); var olan DataTable / sekme düzeni ve `data-dt-primary` işaretleri bozulmaz. Liste ekranı dokunma protokolü (BL-440) bu WP'de tetiklenmez (`Index.cshtml` / `_DataTable.cshtml` / `_Filter.cshtml` / `index.js` değişmiyor); değişirse protokol uygulanır.
- **Sayfa yardımı:** değişmez (K-4, §23 r10).

### 5.2 Sahip / CT kararları (bu WP'ye yazılır)
| # | Karar | Kaynak |
|---|---|---|
| K-1 | Vaka ayrıntısında metin: müşteriler sekmesinde ilk sevkiyat satırının müşteri hücresi, kapsam sekmesinde Shipment kaleminin yer hücresi; rozet altında küçük metin `<div class="small text-muted" data-customer-reason="{değer}">` | Beste "uygun" 2026-11-24 (onay satırı notu) |
| K-2 | İz ekranında `customer.source = Document` düğümünde rozet metni nedenin metni; `customer` taşımayan düğümde genel `Recall.Shipment.Unreadable` kalır | aynı |
| K-3 | Sahip metinleri aynen; zaman bağlamı eklenmez | aynı |
| K-4 | Metni yalnız müşteriler sekmesi, kapsam sekmesi, iz ekranı alır | aynı |
| K-5 | Neden açılışta, `Draft` yeniden izlemede ve revizyon deltasında yazılır, kopyada taşınır | aynı |
| K-6 | `CustomerRow` nedeni **en son yazılan** kalemden (`Revision` → `UpdatedAt ?? AddedAtUtc` → `NodeKey` ordinal); en son kalem nedensizse neden yok | CT, inceleme F-1 (kapandı) |
| K-7 | DL-1 / DL-2 kabul ve tur kayıtları bundan sonra r10'u anar | Beste "uygun" 2026-11-24 |

### 5.3 Açık: S-1 — sözleşme madde 5 açıklama metni (CT bulgusu, bu WP yazılırken ölçüldü)
Paket §8.11 madde 5'teki `CustomerRow.customerSourceReason` açıklaması hâlâ r10 ilk metnindeki kuralı söylüyor: *"the reason of the row's first Shipment item (node-key order) of source Document that carries one; absent otherwise"*. Aynı paketin bağlayıcı kuralı (inceleme F-1, K-6, AC-34 (g), S-34d) ise **en son yazılan kalem, nedensizse yok**. Kod AC'lere göre yazılır; sorun yalnız donacak sözleşmenin açıklama cümlesidir. AC-37 beş maddenin yapısını ölçer, açıklama metnini değil.
- Seçenek A (CT önerisi): madde 5 paketteki metinle **aynen** yazılır (paket yetkisi; onay bozulmaz); fark kabul kaydına "bilinen sapma" olarak geçer ve bir takip maddesi açılır (paket r11 + RECALL 1.11.1 açıklama düzeltmesi, davranış değişmez).
- Seçenek B: açıklama F-1 kuralıyla yazılır — paket metninden sapma; WP paketi aşamaz, bu yüzden önce paket revizyonu ve yeniden onay gerekir (DL-3 gecikir).
- Karar sahibi: Beste (MVP-1 CT). **Cevap gelmeden C1 (sözleşme) commit'i yapılmaz.**

### 5.4 İnceleme bilgi notları (`2026-11-23-6181b276.md`)
- **Bilgi 1 (uygulanır):** AC-34 (g)(i) fixture'ında kopyalanan kalemin `NodeKey`'i delta kaleminin `NodeKey`'inden ordinal olarak **önce** sıralanacak biçimde seçilir — böylece S-34d (g)(i)'de de kesin kırmızı olur. Fixture'da bu sıra bir yorumla sabitlenir.
- **Bilgi 2 (açık, S-2):** aynı `DOC:` anahtarına elle yazılmış daha yeni bir `Manual` Shipment kalemi satırın nedenini gizleyebilir. Paket metni "satırın müşteri anahtarı altındaki Shipment kalemlerinden en son yazılan" der; bu WP **paket metnini aynen** uygular (bütün Shipment kalemleri). İnceleme "WP'de yalnız `Document` kaynaklı kalemler arasından seçilebilir" önerisini CT kararına bırakır — karar gelmeden davranış değişmez; karar "yalnız Document" olursa ek vaka + sabotajla ayrı commit (S-2, sahip Beste).

## 6. Kabul ölçütleri (paket §16 DL-3 — AC metinleri paketten okunur, burada kısaltılmıştır)

| AC | Kısa başlık | Test |
|---|---|---|
| AC-32 | Açılışta neden yazımı: S-U / S-F / S-N → `Unavailable` / `Forbidden` / `NotFound` (Mongo'da dize); `getCaseScope` + `getCaseCustomers` aynı; iki lot aynı izde ortak değer | `RecallCustomerSourceReasonTests.Open_writes_the_read_reason` |
| AC-33 | Neden yalnız okuma hatasında: (a) S-K, (b) S-G, (c) S-L, (d) S-A, (e) S-T, (f) elle, (g) saklı `Crm`, (h) saklı `null`, (i) saklı `Manual` → neden yok | `RecallCustomerSourceReasonTests.No_reason_*` |
| AC-34 | Yeniden izleme ve revizyon (a)…(f); (g) `CustomerRow` en son yazılan kalemden (i)…(iii) | `RecallCustomerSourceReasonTests.Retrace_and_revision_*` |
| AC-35 | Eski vaka: alan yok → altı okuma 200, neden yok, belge baytları ve `UpdatedAt` değişmez, canlı `read = Unavailable` | `RecallCustomerSourceReasonTests.Legacy_case_has_no_reason` |
| AC-36 | Kiracı: T2 → `NotFound`, T1 bilgisi yok; çapraz okuma 404; MVP-6 isteği çağıranın `Authorization`'ı; `RecallTenantBoundaryTests` değişmeden yeşil | `RecallCustomerSourceReasonTests.Tenant_*` |
| AC-37 | RECALL 1.11.0: fark tam beş madde (`JsonNode.DeepEquals`), fixture bayt eşit, yanıtlar `RecallContractValidatingHandler` altında, `Validated` sayacı `getCaseScope` / `getCaseCustomers` > 0 | `RecallContract1110Tests.*` |
| AC-38 | Dondurulmuş vektörler bayt eşit: rs-scope-v1, rt-v1, AC-2 (a), AC-10 (f), AC-15, AC-26 (d); denetim ayrıntısında neden yok | `RecallCustomerSourceReasonTests.Frozen_vectors_*` + mevcut vektör testleri değişmeden |
| AC-39 | Üç metin ekranda (tr): müşteriler, kapsam, iz rozeti; teslim sütunu değişmez | `recall-fu01-reason.test.mjs` "three reasons, three texts" |
| AC-40 | Neden yoksa metin yok (a)…(e); canlı okumadan türetme yok | `recall-fu01-reason.test.mjs` "no reason, no text" |
| AC-41 | 7 dil: üç anahtar dolu, tr harfiyen, her dilde üçü farklı, `zh`/`ar`/`ru` kopya değil; `ar` çizimi | `RecallFu01ReasonWebTests.Seven_languages_*` + "texts come from l10n" |

## 7. Sabotaj listesi (paket §16 / §17.1 r10 — tam liste, 35 kalem)

Her sabotaj ayrık worktree'de, son commit üzerinde: tek değişiklik → hedef test **kırmızı** → bayt-eşit geri al → **yeşil**, `git diff --stat` boş. Hiçbiri commit'e girmez. Kırmızıya dönmeyen sabotaj = bekçi değil; dur, CT'ye yaz.

| S | Bozulan şey | Beklenen kırmızı |
|---|---|---|
| S-32a | `ScopeItems` nedeni yazmasın | AC-32 |
| S-32b | `ScopeItems` her başarısız okumaya `Unavailable` yazsın | AC-32 `Forbidden` / `NotFound` kalemleri |
| S-32c | `RecallReads.Item` kaleme nedeni koymasın | AC-32 `getCaseScope` |
| S-32d | `GetRecallCaseCustomersHandler` satıra nedeni koymasın | AC-32 `getCaseCustomers` |
| S-33a | `ScopeItems` `Document` kalemine okuma sonucunu koşulsuz yazsın | AC-33 (a)(b)(c) (+ AC-37 şema) |
| S-33b | `CustomerKey` `Crm` dalında izlenen nedeni yazsın | AC-33 (g) |
| S-33c | `null` kaynak dalında izlenen nedeni yazsın | AC-33 (h) |
| S-33d | `Manual` dalında izlenen nedeni yazsın | AC-33 (i) |
| S-34a | `CustomerKey` varsayılan dalında nedeni kopyalamasın | AC-34 (a)(c) |
| S-34b | Reconcile insert'inde nedeni temizlesin | AC-34 (e) |
| S-34c | revizyon kopyası (`R3Handlers.cs:555-563`) nedeni taşımasın | AC-34 (f) |
| S-34d | `CustomerRow` kuralını "`NodeKey` sırasıyla ilk dolu kalem"e döndür | AC-34 (g)(i)(ii) |
| S-35a | `GetRecallCaseCustomersHandler` eksik nedeni canlı `lines[].Read`'den doldursun | AC-35 (i) |
| S-35b | `RecallReads.Item` alan yokken `Unavailable` varsaysın | AC-35 |
| S-36 | `RecallServiceCollectionExtensions.cs`'ten `.AddHttpMessageHandler<ForwardCallerHeadersHandler>()` kaldır | AC-36 (c) |
| S-37a | sözleşmede `CustomerSourceReason` enum'una `Ok` ekle | AC-37 fark testi |
| S-37b | `customerSourceReason`'ı `CustomerDisplay`'e de ekle | AC-37 fark testi |
| S-37c | değişiklik notunun ilk satırını "1.11.0 — additive only" yap | AC-37 |
| S-37d | sunucu `RecallReads.Item`'da `customerSourceReason = "Ok"` yazsın | AC-37 `getCaseScope` şema doğrulaması |
| S-38a | `RecallScopeRules.Canonical`'a `customerSourceReason` yaz | AC-38 (a) + AC-15 vektörü |
| S-38b | kanıt paketi müşteri hücresine neden metnini ekle | AC-38 (c) |
| S-38c | `exportTrace` "place" hücresine okuma nedenini ekle | AC-38 (c) + AC-10 (f) |
| S-38d | `submitScope` denetim ayrıntısına nedeni ekle | AC-38 (d) |
| S-39a | üç değeri tek anahtara eşle | AC-39 (a) |
| S-39b | metni `Recall.Read.{değer}` anahtarından al | AC-39 (a) `Forbidden` hücresi |
| S-39c | kapsam sekmesi nedeni çizmesin | AC-39 (b) |
| S-39d | `trace-view.js:65` `Document` düğümünde de genel rozeti bassın | AC-39 (c) |
| S-40a | metni canlı `r.shipments[0].read`'den türet | AC-40 (c) |
| S-40b | `customer.source = Document` denetimini kaldır | AC-40 (d) |
| S-40c | alan yokken `Unavailable` varsay | AC-40 (a)(b) |
| S-40d | bilinmeyen değerde `t('Recall.Customer.SourceReason.' + değer)` bas | AC-40 (d) |
| S-41a | `Recall.ru.resx`'ten `…NotFound`'u sil | AC-41 |
| S-41b | `Recall.zh.resx`'te `…Forbidden`'ı İngilizce kopya yap | AC-41 |
| S-41c | `Recall.cs` listesinden `…Unavailable`'ı çıkar | AC-41 |
| S-41d | `case-detail.js`'e "Sevkiyat bulunamadı" sabit yaz | AC-41 `ar` çizimi |

S-37a…S-37c sözleşme dosyasına uygulanır ve geri alınır (r9 S-17 deseni). S-36 başka DL'nin dosyasına geçici dokunur — sabotajdır, değişiklik değildir.

## 8. Test koşuları

- **Dilim başı ölçümü (Adım 0):** Inventory ve Web.Tests geçti / kırmızı sayıları ve kırmızıların adları; vitest sayıları; aynı gün `origin/main`'de taban (ayrık worktree).
- **Inventory test paketi bütünüyle, filtre yok:** `dotnet test services/Diten.InventoryService/tests/Diten.InventoryService.Tests` — servis dokunulduğu için (§17, CT kuralı). Suite kendi replica set'ini açar: `INVENTORY_TEST_MONGO` **ayarlanmaz**, **27017 kullanılmaz**. Bu makinede aynı anda tek Inventory koşusu; çıktı dosyaya yönlendirilir (boru yok); Python koşucuları UTF-8 (`PYTHONUTF8=1`).
- **Web:** `dotnet test frontend/Diten.Web.Tests`, filtresiz (Node testleri dahil). `RecallFu01NodeTests`, `RecallFu01ReasonWebTests`, `RecallCustomerPageWebTests`, `RecallContractFakesTests`, `EvidenceTextParityTests`, `RecallWebTests` yeşil.
- **vitest:** Windows'ta bilinen kırmızılar dal ve tabanda adlarıyla karşılaştırılır; yeni kırmızı 0 (node_modules junction ve UTF-8 ortamı: UIC notu).
- **resx-check:** kiracı bağlamı, 7 dil, `Recall.*`.
- **Build:** `Diten.InventoryService` ve `Diten.Web` PASS, uyarı politikası değişmeden.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**.
- Değişmezlik kanıtları: `git diff <dilim-başı>..<dilim-sonu> --stat -- <§3 "Değişmez" listesi>` boş; `tests/architecture/audit-ledger/` farkı boş.

## 9. Git kuralları (GIT-002)

- Dal: `feature/sce/mod-0173-inventory-ledger-valuation`; yeni dal yok; `main`'e commit / push yok.
- Stage yalnız açık yolla (`git add -A` / `git add .` yok); commit'ten önce `git diff --cached --stat` yalnız bu WP'nin dosyalarını gösterir.
- Hook atlanmaz (`--no-verify` yok, `core.hooksPath` değişmez). Reset, amend, rebase, squash yok. **Push yok, PR yok.**
- Üç commit (C1 → C2 → C3); ara commit'te tam takım kırmızı kalmaz. Örnek iletiler: `docs(contract): RECALL 1.11.0 — MOD-0177-FU01 DL-3 customerSourceReason (additive)`, `feat(sce): MOD-0177-FU01 DL-3 — the scope item keeps why the customer fell back to the document key`, `feat(web): MOD-0177-FU01 DL-3 — three read-reason texts on case detail and trace (7 languages)`.

## 10. Canlı tur (CT yapar; geliştirici yapmaz)

Gateway 5000 / Web 5001 üzerinden, oturumdaki kullanıcıyla. Diller tr / en (ve `ar` RTL kontrolü), 375 px. Vaka ayrıntısı müşteriler + kapsam sekmeleri ve iz ekranı; UIC seçici (kabul kapısı (6)). E5 smoke (§17): MVP-6 okuması `Unavailable` iken açılan vakada "Sevkiyat servisine erişilemedi" görünür (RC-2026-0003'ün soğuk ilk okuma durumu).

## 11. Teslim raporu

1. Adım 0: etiket sonrası HEAD, merge türü (ata / ff / no-ff), paket ve `recall.openapi.yaml` sha256.
2. S-1 kararı ve uygulanan metin (madde 5); S-2 durumu.
3. Commit SHA'ları ve iletileri (`git show --stat` her biri).
4. AC tablosu: `| AC | test adı | dosya:satır | sonuç |`.
5. Sabotaj tablosu (35 kalem): `| S | dosya:satır, değişiklik | kırmızı test / çıktı özeti | geri alındı (diff boş) |`.
6. Test sayıları dal / taban: Inventory, Web.Tests, vitest, resx-check; taban kırmızıları adıyla; koşu tarihi / saati, ayrık worktree yolları ve SHA'lar.
7. `README.md` iki satırı ve fixture LF sha256'sı.
8. Eşzamanlı işlerle çakışma (README, `Recall.*.resx`) ve çözümü.
9. Ölçülmeyenler: canlı tur ve UIC (CT), E5 smoke. Uydurma yok — ölçülmeyen "ölçülmedi" diye yazılır.

## 12. Kural haritası (AGENTS.md §6.1)

Daima: `multi-tenancy`, `security-jwt`, `git-safety`, `code-style`, `docs-organization`, `cross-module-change-consent` (XMC-001). Backend: `handler-design`, `repository-standard`, `response-envelope`, `entity-base-template`, `api-conventions`, `audit-trail-standard` (AUD-001, değişmezlik). Sözleşme: `api-conventions` + `docs/analysis/contracts/README.md` kayıt kuralı. Ekran: `localization-standard` (kiracı 7 dil), `frontend-datatable-template` (dokunma protokolü yalnız liste dosyası değişirse), `views-organization`. Kanıt: `status-reporting-and-evidence` (REP-001). Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.
