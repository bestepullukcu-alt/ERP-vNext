# WP-SYNC-144 — PR #144 sonrası CRM / MDM uyum borçları (BL-640 / 641 / 642 + W3E sayfaları)

> **Kaynak:** PR #144 (Kapanış hattı) main'e girdi (`6b8a62d5a`). Test dalına senkron: `f8750edc7` (2026-10-16).
> Borç defteri: `docs/roadmap/backlog/product-backlog.md` BL-640 / BL-641 / BL-642. **Süre: 2026-11-13** (muafiyetler biter, CI kırmızıya döner).
> Yol haritası: [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md) sıra 7b.

## Kapsam ve CT ölçümü (2026-10-16)
- **BL-642:** 5 kiracı sayfasının "?" yardım metni yok: COMPETITORPRODUCTS, CRMDOCTORINSIGHTTHRESHOLDS, OBJECTIONRESPONSES, VISITEVIDENCESETTINGS, VISITWORKSPACE.
  - `frontend/Diten.Web/tests/page-help-ledger.test.js` → `MAIN_SYNC_3_PAGES`.
- **BL-641:** 3 yan panelin birincil düğmesinde `data-dt-primary` yok.
  - `CRM/KnowledgePaths/_PresentationNotes.cshtml`, `CRM/PotentialThresholds/Index.cshtml`, `CRM/VisitEvidenceSettings/Index.cshtml`.
  - Muafiyet: `tests/diten-enter-primary.test.js` (satır ~454-456, ~498-500).
- **W3E (deftere hiç girmedi):** CRMVISITQRCODES ve CRMVISITEVIDENCEFLAGS `CrmManifestProvider`'da kayıtlı.
  - `PageHelpResource`'ta metinleri yok, `MAIN_SYNC_3_PAGES`'te de değiller → `page-help-ledger.test.js` CI'da büyük olasılıkla kırmızı.
  - Bu makinede vitest çalışmıyor (Node 16), yerelde ölçülemiyor.
- **W3E-E4R2 ile gelen yeni paneller / pencereler:**
  - Ziyaret kanıtı: yeni istisna çekmecesi, hızlı görünüm eylemleri, kaydet onayı.
  - Eşikler: düzenleme çekmecesi + kaydet onayı.
  - Kurum QR kodları: oluştur / iptal / yazdır pencereleri.
- **K-TABS `diten-tabs.js`:** sekme şeridinde ← / → / Home / End için kendi `keydown` dinleyicisi var. Kural §3: "sayfa kendi `keydown` dinleyicisini yazmaz; kısayol `DitenShortcuts` ile". Ortak bir bileşen (WAI-ARIA tablist), sayfa dinleyicisi değil. Ama `diten-shortcuts.test.js` (vitest) bunu yakalayabilir → ölçülmeli.

## §36.1 Agent Prompt — W-UXH (paste-ready · yalnız Web · BL-640 ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md] @[.antigravity/agents/l10n-agent.md]
WP: WP-SYNC-144 · W-UXH — UXH-001 uyumu: 7 CRM kiracı sayfasına "?" yardım metni (7 dil) + CRM yan panelleri / pencereleri için Enter / chord işareti; BL-642 ve BL-641 muafiyet satırları çıkar
Repository: C:\tmp\sync144-uxh (worktree, test/crm-content-visit-e2e güncel başı — main 3912d568b senkronlu) · Branch: wp/sync144-uxh · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-SYNC-144-uxh-mdm.md (kapsam ve CT ölçümü). Kural: .antigravity/rules/page-help-and-keyboard.md (UXH-001 — tamamını oku: §1 Enter / chord, §2 yardım anahtarları, §3 kısayol katmanı, tablo "birincil işlem türleri"). Borç: docs/roadmap/backlog/product-backlog.md BL-641 / BL-642. Örnek: PageHelpResource'ta metni olan bir kiracı sayfası (ör. USERS / TASKS) + PageHelpCatalog adım ikonları.
MEVCUT: Resources/PageHelpResource.{7 dil}.resx · Services/Help/PageHelpCatalog.cs · tests/page-help-ledger.test.js (MAIN_SYNC_3_PAGES) · tests/diten-enter-primary.test.js (BL-641 adlı satırları) · tests/diten-shortcuts.test.js · tests/page-help-rule-contract.test.js · wwwroot/assets/js/diten-tabs.js (K-TABS, kendi keydown'u).
NE:
(1) Yardım metni (BL-642 + W3E): COMPETITORPRODUCTS, CRMDOCTORINSIGHTTHRESHOLDS, OBJECTIONRESPONSES, VISITEVIDENCESETTINGS, VISITWORKSPACE, CRMVISITQRCODES, CRMVISITEVIDENCEFLAGS — her biri için Help.{KOD}.Purpose, .Audience, (gerekiyorsa .Immediate), Step1..n (en çok 5) + Step{n}Title; 7 dil (en, tr, fr, es, zh, ar, ru — kiracı modülü); adım ikonları PageHelpCatalog'da. METİN SAYFANIN BUGÜNKÜ DAVRANIŞINI anlatır (WP-VW-W3 / W4 / W3E belgeleri ve §37 notları: ör. Ziyaret kanıtı = kiracı ayarı + iç sekmeler + süreli istisna Workflow onayı; Kanıt işaretleri = işaret ipucudur ihlal değildir, 92 gün, temsilci özeti; Kurum QR = kod oluştur / iptal / yazdır, şehir süzgeci; çalışma alanı = takvim + yürütme + QR / konum kanıtı); tırnaklı düğme / alan adları ekrandaki metinle birebir. Yeni metin yazarken mevcut çevirileri değiştirme. MAIN_SYNC_3_PAGES'ten 5 satır çıkar (liste boşalırsa nesneyle birlikte kaldır — testin "yalnız küçülür" kuralına uy); W3E iki sayfası deftere HİÇ girmez (metinle gelir).
(2) Enter / chord (BL-641 + yeni paneller): kural tablosuna göre her CRM yan panelinin / penceresinin birincil düğmesine data-dt-primary: kaydet / oluştur / onay penceresi açan → "enter"; sonuçlu (yayımla, onaya gönder, onayla, iade, iptal, erken bitir, QR iptal, "Kaydet ve uygula" — tüm temsilcilere hemen uygulanır) → "chord" (aria-keyshortcuts + ipucu kuralın §1'i). Kapsam: KnowledgePaths _PresentationNotes; PotentialThresholds düzenleme çekmecesi + kaydet onayı; VisitEvidenceSettings ayar formu + kaydet onayı + yeni istisna çekmecesi + istisna hızlı görünüm eylemleri; VisitQrCodes oluştur / iptal / yazdır (tekli + toplu). Her karar raporda tabloyla (panel · düğme · enter/chord · gerekçe). diten-enter-primary.test.js'ten BL-641'in 3 adlı satırı çıkar.
(3) diten-tabs.js ve kısayol kuralı: diten-shortcuts.test.js / page-help-rule-contract.test.js'in keydown kuralı bu dosyayı yakalıyor mu ÖLÇ. Yakalıyorsa: kuralın izin verdiği yol (DitenShortcuts.register ya da kuralın ortak bileşen istisnası) — kendi başına yeni istisna satırı EKLEME; çözüm kuralın içinde yoksa DUR, raporla (karar kullanıcı / Kapanış). Yakalamıyorsa raporda belirt.
KORU/YAPMA: sayfa davranışları, K-STD / K-TABS / K-THEME aynen; ortak dosyalar (_Layout, diten-shortcuts.js, diten-datatable.js, backbone-custom.css, _TabCard) DEĞİŞMEZ (XMC-001; gerekirse DUR, raporla); başka modülün yardım metni / satırı DEĞİŞMEZ; defterde YENİ muafiyet satırı EKLEME; Help.Panel.* ortak anahtarlarına dokunma; kalıcı veri yazma YOK; kullanıcının sekmesine harness yok.
DOĞRULA: vitest bu makinede çalışmaz (Node 16) → page-help-ledger, diten-enter-primary, diten-shortcuts, page-help-rule-contract testlerinin bu sayfalara düşen ölçümlerini Node 16'da çalışan bir xUnit + RunNode testiyle ya da testlerin kendi tarama kodunu çağıran küçük bir Node betiğiyle yeniden üret (üretim dosyalarını okuyarak) ve sonucu raporla; CI asıl hakemdir. resx: 7 dilde anahtar tamlığı (Purpose / Audience / Step{n} + Title, boşluksuz sıra), değer ≠ anahtar, en ≠ tr. Web.Tests (xUnit) tam takım (-o repo içi), taban karşılaştırması + 3 sabotaj (.bak + touch): (1) bir sayfanın tr Step2Title'ı silinsin → kırmızı; (2) VisitEvidenceSettings "Kaydet ve uygula" chord → enter → kırmızı; (3) MAIN_SYNC_3_PAGES'e satır geri eklensin (defter büyür) → kırmızı. Görsel kanıt (AYRI sekme; fleet açıksa): 2 sayfanın "?" paneli (tr + ar), bir chord düğmesinin ipucu.
Commit: "fix(web): WP-SYNC-144 W-UXH — page help for 7 CRM tenant pages (7 languages), Enter / chord marks on CRM side panels and dialogs; BL-642 and BL-641 ledger rows removed" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```
