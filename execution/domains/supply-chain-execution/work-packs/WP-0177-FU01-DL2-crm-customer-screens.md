# WP-0177-FU01-DL2 — Geri çağırmada CRM müşteri kimliği DL-2 (ekran): r9'a göre yeniden doğrulama, F-1 kapanışı, eksik kalırsa tamamlama

Tarih: 2026-11-13 · Veren: CT (merkez, Beste adına) · Sahip kararı: "DL-2'yi başlat" (Beste, 2026-11-13) · Paket:
`execution/domains/supply-chain-execution/module-packs/MOD-0177-FU01-recall-crm-customer-ref.md` **r9**, LF sha256
`c12df7b73a26f9bb2fb9a6ff18f40e184a75b4ae6435aa07672b044736896aaf` (`MSYS_NO_PATHCONV=1 git show HEAD:<yol> | sha256sum`, `cd8e2641f`'de
ölçüldü). Onay satırı `execution/approvals/approvals.tsv`, Ali (GMG-ERP), `ac493e735`. AC-17 VERIFIED
(`docs/records/audits/2026-11/mvp1-ct-acceptance-batch-2026-11-13.md` §3).

## 0. Önce bunu bil: DL-2 kodu zaten var (ölçüldü, 2026-11-13)

- DL-2, paket **r8**'e göre `a872e8c56` ile yazıldı (2026-11-05, 13 dosya, hepsi §5 DL-2 listesinde). CT 2026-11-06'da DL-1 ile birlikte
  **VERIFIED** dedi: `docs/records/audits/2026-11/mod-0177-fu01-dl1-dl2-ct-acceptance-2026-11-06.md`. Kod `origin/main`'de (PR #424, `6ef2c884b`).
- r8 → r9 farkı (`git diff eb27a299c ea0ed1c8a`) yalnız künye, AC-17, §17, §17.1, §18, §19 notları ve §22 sonu. DL-2'nin bölümleri
  değişmedi: §5 DL-2, §8.8, §9, §11, §16 DL-2 (AC-21…AC-24, AC-27, AC-31), §23.
- `a872e8c56`'dan sonra DL-2 dosyalarında yalnız `PageHelpResource.tr.resx` değişti (başka modüllerin yardım satırları).
  `Help.RECALLCASEDETAILS.Step4` yedi dilde duruyor.
- **Açık bulgu F-1 (DÜŞÜK, 2026-11-06 kaydı):** `case-detail.js` `RecallCustomerView.text` (`:17`, `:22`, `:26`, `:30`) Legacy / NoCrm
  etiketindeki `"D"` biçimli GUID'i süzer, ama bunu sınayan test yok. CT-4 sabotajı (`:30` süzgecini kaldır) **yeşil** kaldı (320 / 320).
- **Bu yüzden bu WP sıfırdan geliştirme değildir.** Üç iş yapılır:
  1. Mevcut DL-2'yi bugünkü hatta r9'a göre yeniden ölç.
  2. F-1'i bir testle kapat.
  3. Ölçümde eksik çıkan bir AC maddesi olursa yalnız onu tamamla.
- Kodda hazır bulunan hiçbir şey yeniden yazılmaz.

## 1. Başlamadan

- **Dal:** tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`, ana checkout `C:\Users\Ali\erp-vnext`. Yeni dal açılmaz.
- **CT etiketi `ct/line-ff-2026-11-13r`:** etiket HEAD'in atasıysa devam. Değilse `git merge --ff-only ct/line-ff-2026-11-13r`. ff olmazsa
  `git merge --no-ff ct/line-ff-2026-11-13r -m "merge: CT chain ct/line-ff-2026-11-13r into MVP-1 line (CT standing approval, docs/records only)"`.
  Çakışmada `git merge --abort`, dur, CT'ye yaz. Etiket yoksa dur. Rebase yok.
- **Paket:** sha256'yı hat dalından ölç (`c12df7b7` ile başlamalı); değilse dur.
- **Eşzamanlılık:**
  - Aynı ana checkout'ta MOD-0178-FU01 DL-2 (Web vekili: `OutboundDtos.cs`, `OutboundShipmentsController.cs`, Web.Tests) ve başka Web
    işleri sürüyor.
  - Ana checkout'ta derleme, test ve sabotaj yok; Inventory 5067 orada `dotnet watch` ile koşuyor.
  - Ölçüm ayrık worktree'de yapılır: `git -C C:\Users\Ali\erp-vnext worktree add --detach C:\Users\Ali\erp-vnext\.claude\worktrees\verify-0177-dl2 <SHA>`.
  - Taban için ikinci ayrık worktree açılır: `…\verify-0177-dl2-base`, etiket üzerinde.
  - Commit'ten hemen önce hattı yeniden al. `Recall.cs`, `Recall.*.resx`, `PageHelpResource.*.resx` başka işte de değiştiyse ikisini de koru
    ve CT'ye yaz.
- **Oku:**
  - Paket §5 "DL-2" (`:234-248`), §6 tablosu (`RecallContractFakes`), §8.8 Gösterim kuralı, §9 Layout & Shell Contract.
  - §11 Frontend File Contract, §16 DL-2 (AC-21…AC-24, AC-27, AC-31 ve sabotajları), §17.1, §22 (`:1466` dilim satırı, DL-2 önkoşulu),
    §23 Page Help.
  - 2026-11-06 CT kaydı (F-1, CT-3, CT-4).

## 2. Önkoşullar (ölçüldü, `cd8e2641f`)

- **DL-2 önkoşulu, RECALL 1.10.0 canlı dosyada:** `docs/analysis/contracts/recall.openapi.yaml`.
  - `:14` `version: 1.10.0`; `:19` 1.10.0 notu "additive + one input restriction (MOD-0177-FU01".
  - `CustomerDisplay` `:1580`: `required [source, state]`, `additionalProperties: false`, yedi `state`.
  - `customer` üyesi beş şemada: `:1228` EffectivenessRow, `:1337` ScopeItem, `:1495` TraceNode, `:1559` ReturnLink, `:1608` CustomerRow.
  - `components/parameters/CustomerPage` `:1032`; altı okumada başvuru: `:91`, `:180`, `:192`, `:454`, `:531`, `:582`.
  - `ServiceUnavailable` `:1836`.
  - İki kod üst düzey kayıtta: `:991` `…_FORBIDDEN: 403`, `:1011` `…_UNAVAILABLE: 503`.
  - Web sahtelerinin 2xx gövdeleri (`RecallContractFakes.Bound`) buna bağlanabilir.
- **DL-1 kodu main'de:** PR #424, `6ef2c884b`. DL-1'in commit'i `f57172eed`, DL-2'nin `a872e8c56`; ikisi de `origin/main`'in atası.
- **AC-17:** VERIFIED (2026-11-13).

## 3. Kabul kapıları (DL-2 kodlamasını ve ölçümünü bekletmez, ACCEPTED'ı bekletir)

- **D-1:** SHIPMENT-BUNDLE 3.5.0 FROZEN; AC-17 Skip'siz yeşil.
- **D-2:** CRM U-1 FROZEN ve uygulandı.
- **D-3:** kullanıcı token'ı (Q-1). CT teyidi kabul kaydına yazılır.
- **D-4:** `crm.account.lookup-validation` rol eşlemesi (Ali, MOD-0018, XMC-001). DL-1 ACCEPTED'ı bekletir, DL-2 işini bekletmez.
  Eşleme yoksa CT canlı turu yönetici rolüyle yapar ve bunu kayda yazar.
- **AC-20 (f):** veri sayımı = 0 ve **AC-30** rol kontrolü (§22). F-1'in DÜŞÜK kalması AC-20 (f)'nin 0 çıkmasına bağlı. Sayım 0 değilse
  F-1'in önceliği CT kararıdır.
- **AC-30 (3), birlikte açılış:** DL-1 kullanıcıya açık ortama tek başına çıkmaz. DL-2 kabulü ve iki commit aynı geçiştedir.

## 4. Kapsam (paket §5 "DL-2" listesi dışına çıkma)

- Dosyalar:
  - `frontend/Diten.Web/wwwroot/assets/js/Recall/case-detail.js`, `trace-view.js`, `trace.js`, `recall.l10n.js`.
  - `evidence.js` ve `drill-view.js` **değişmez**.
  - `frontend/Diten.Web/Controllers/RecallController.cs`, yalnız şu noktalar:
    - `CaseData` `customerPage` iletimi (bugün `:250`).
    - `TraceData` (bugün `:116`).
    - `SendFileAsync` `Accept-Language` (bugün `:869`).
  - `frontend/Diten.Web/Resources/PageHelpResource.{en,tr,fr,es,zh,ar,ru}.resx`, yalnız `Help.RECALLCASEDETAILS.Step4`.
  - `frontend/Diten.Web.Tests/JavaScript/Node/recall-fu01.test.mjs`, `frontend/Diten.Web.Tests/Recall/RecallFu01WebTests.cs`
    (`RecallFu01NodeTests`, `RecallCustomerPageWebTests`).
- **Metin anahtarları DL-1'de eklendi** (§11; ölçüldü: `Recall.cs` ve yedi `Recall.*.resx` dosyasının her birinde 22 `Recall.Customer.*`,
  iki `Recall.Errors.RECALL_CUSTOMER_DIRECTORY_*`). DL-2 bunları yalnız kullanır.
  - Eksik çıkarsa ekleme DL-2'de yapılır, yedi dilde.
  - Ortak anahtarda parite bekçisi (`EvidenceTextParityTests`) ve servis ailesi bozulmaz.
- **Servis değişmez.** InventoryService, `recall.openapi.yaml`, `ocelot.json`, CRM / SupplyChain / Auth, `shipment-bundle.openapi.yaml` ve
  MOD-0173…0176 dosyalarına dokunulmaz.
  - Servis tarafında boşluk görülürse dokunma, CT'ye yaz (XMC-001 / DL-1 konusu).
- **F-1 düzeltmesi:**
  - `recall-fu01.test.mjs`'e bir vaka eklenir: `customer = {source: Unknown, state: Legacy, label: <"D" biçimli GUID>}` ve
    `{source: Manual, state: NoCrm, label: <"D" biçimli GUID>}`.
  - Beklenti: sayfa metninde ve `title`'da GUID yok, rozet ve ipucu var.
  - Sabotaj CT-4 (`case-detail.js:30` `GUID.test(c.label) ? '' : c.label` → `c.label`) bu testle **kırmızı** olmalıdır.
  - Üretim kodu bu düzeltme için değişmez.

## 5. Kurallar

- **Gösterim kuralı (§8.8):**
  - GUID hiçbir metinde, `title`'da ve araç ipucunda görünmez. Yalnız `data-*` / `option.value`'da taşınabilir.
  - `Resolved` → "{ad} · {kod}". `Unresolved`, `Forbidden`, `NotFound` ayırıcıyla gösterilir. `Deferred` yalnız komutsuz tablolarda ve iz
    ekranındadır.
  - `Unresolved` ve `Forbidden` satırda komut düğmesi yoktur, yerine `CrmUnavailable` / `CrmForbidden` yazılır. Bu müşteriler seçim
    listesinde yer almaz.
  - İz ekranında `Forbidden` düğüm varsa CSV bağlantısının yerine `Recall.Customer.ExportForbidden` yazılır.
- **Kiracı:** başka kiracının vakası ya da izi 404 döner (vekil servisin durumunu korur, `Refuse`). Vekilde sabit ya da varsayılan kiracı yok.
- **UAS-001:** yetkisiz kullanıcıya sayfa iskeleti çizilmez, yönlendirme yapılmaz. Mevcut `[HasPermission]` ve görünüm kapısı değişmez;
  yeni eylem ve yeni izin yok.
- **Dil:** hardcoded metin yok (AC-24). Kiracı modülü 7 dil: en, tr, fr, es, zh, ar, ru. `zh` / `ar` / `ru` İngilizce kopya olamaz.
- **Golden / BL-586:**
  - Layout & Shell Contract: yeni sayfa, yeni modal, yeni form alanı yok (`form_field_count: 0`).
  - Rozetler mevcut `badge bg-label-*` sınıflarıyla.
  - Mevcut DataTable / offcanvas düzeni ve `data-dt-primary` işaretleri bozulmaz.
- **Page Help (UXH-001):** yalnız Step4 değişir, üç cümle (§23). Kanıt ve iz ekranlarının yardımı değişmez.
- **Korunan yollar:** `_Layout.cshtml` donuk; `.antigravity/**` ve `ocelot.json` değişmez. Başka modülde düzeltme gerekirse önce sor (XMC-001).
- **Git:**
  - Stage yalnız açık yolla (`git add -A` / `git add .` yok). Commit'ten önce `git diff --cached --stat` yalnız bu WP'nin dosyalarını göstermeli.
  - Hook atlanmaz: `--no-verify` yok, `core.hooksPath` değiştirilmez ya da boşaltılmaz.
  - Reset, amend, rebase ve squash yok.
  - **Push yok, PR yok.**

## 6. Yapılacaklar

1. **Boşluk ölçümü (ayrık worktree, HEAD):** AC-21…AC-24, AC-27, AC-31'in her maddesini koddaki karşılığıyla eşle (dosya:satır, test adı).
   - §8.8 "Görünen yerler" listesini tek tek kontrol et: kapsam, fark, müşteriler, iade bağları, etkinlik, üç pencere, seçim listesi, iz.
   - Eksik madde yoksa üretim kodu değişmez.
2. **F-1 testi** (§4): ayrı commit. Örnek mesaj:
   `test(web): MOD-0177-FU01 DL-2 F-1 — a GUID-shaped legacy / manual label is never shown (CT-4 red)`.
3. **Eksik çıkarsa tamamlama:** her eksik ayrı commit, yalnız §4 dosyalarında. Commit mesajında AC numarası yazılır.
4. **Sabotajlar** (ayrık worktree, son commit üzerinde): her biri tek değişiklik → kırmızı → bayt-eşit geri al → yeşil, `git diff --stat` boş.
   - Paketteki liste: S-21; S-22a…S-22d; S-23a…S-23e; S-24a, S-24b; S-27a…S-27g; S-31a…S-31d.
   - Ek olarak CT-4 (F-1).
   - S-27c / S-27d `Recall.cs` listesine uygulanır; S-31c / S-31d Web vekiline uygulanır.
5. **Test koşuları** (ayrık worktree; dal ve taban aynı gün):
   - `dotnet test frontend/Diten.Web.Tests`, **filtresiz**. Bilinen 3 main kırmızısı adıyla yazılır; yeni kırmızı olmamalı.
     `RecallFu01NodeTests`, `RecallCustomerPageWebTests`, `RecallContractFakesTests`, `EvidenceTextParityTests` yeşil olmalı.
   - **vitest:** Windows'ta bilinen 13 kırmızı. Dal ve taban adlarıyla karşılaştırılır; yeni kırmızı olmamalı.
     node_modules junction ve UTF-8 ortamı için bkz. UIC notu.
   - **resx-check:** kiracı bağlamı, 7 dil, `Recall.*` ve `PageHelpResource` Step4.
   - `Diten.AuthService.AccountKindAcceptanceHost.Tests` **koşulmaz**. InventoryService takımı servis değişmediği için zorunlu değildir;
     servis dosyası değişirse dur.
6. **Kapanış:** iki ayrık worktree kaldırılır. Ana checkout'ta `git status --short` bu WP'den commit'lenmemiş dosya göstermez.

## 7. Canlı tur (CT yapar; geliştirici yapmaz)

- Gateway 5000 / Web 5001 üzerinden, oturumdaki kullanıcıyla (E3). Diller en / tr / ar (RTL), genişlik 375 px.
- Kapsam: vaka detayı müşteriler, kapsam, iade ve etkinlik sekmeleri, üç pencere, iz ekranı (CSV bağlantısı), Step4 yardımı.
- D-4 kapanmadıysa tur yönetici rolüyle yapılır ve bu kayda geçer.

## 8. Rapor

1. Adım 0 / 1: etiket sonrası HEAD, merge türü (ata / ff / no-ff), paket sha256.
2. Boşluk tablosu: `| AC maddesi | dosya:satır | test adı | durum (vardı / eklendi) |`.
3. Commit SHA'ları, mesajları, her biri için `git show --stat`.
4. Sabotaj tablosu: `| S-n | dosya:satır, değişiklik | kırmızı test / hata satırı | geri alındı (diff boş) |`.
5. Test sayıları, dal ve taban:
   - Web.Tests toplam / geçen / kalan / atlanan, taban kırmızıları adıyla.
   - vitest sayıları ve 13 kırmızının adları.
   - resx-check çıktısı.
   - Koşu tarihi ve saati, ayrık worktree yolları ve SHA'lar.
6. Eşzamanlı işlerle (MOD-0178-FU01 DL-2 ve öteki Web işleri) çakışma oldu mu, nasıl çözüldü.
7. Ölçülmeyenler ve açık noktalar (canlı tur CT'de, D-4, AC-20 (f), AC-30, push yok). Uydurma yok: ölçülmeyen "ölçülmedi" diye yazılır.
