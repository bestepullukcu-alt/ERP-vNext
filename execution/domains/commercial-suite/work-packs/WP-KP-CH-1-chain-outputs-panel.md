# WORK PACKAGE — WP-KP-CH-1 (eski SB-2b) · Zincir şablonu "Çıktılar" paneli: bu zincirden kurulan Bilgi Yolları, onları kullanan yolculuklar ve planlanan ziyaretler

> **CT (SoR), 2026-10-01.**
> - **Yol haritası:** `ROADMAP-content-to-visit.md` Faz 1. Kullanıcı (2026-10-01): "SB-2b ve kod temizliği paketle".
> - **Yeniden tanım:** SB-2b, SB-2'nin (set yayını) geri izini okuyacaktı; SB-2 KP-4'te emekli oldu. Artık zincir → **Bilgi Yolu** (KP-1 `ChainRef`) → **yolculuk aşaması** → **planlanan ziyaret** (`ContentItems[]`, SB-3b) zincirini gösterir.
> - **Kapsam:** CrmService (tek okuma ucu) + Web (panel). Yazma YOK.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-ch-1`, dal `wp/kp-ch-1`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Amaç
Zincir şablonunu düzenleyen kişi, o zincirin sahada nereye indiğini görsün:
- bu zincirden kurulan **Bilgi Yolları** (ülke, dil, sürüm, yol durumu, son revizyon / MLR durumu, yayında mı);
- o yolları aşamalarında kullanan **Etkileşim Yolculukları**;
- o yolları taşıyan **planlanan ziyaret** sayısı (gelecek, iptal edilmemiş).

## Kanıt (CT)
- **Panel bugün boş:** `frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js:427-428` ("Outputs … have no reverse reference to a chain template today, so they show —"), `renderConnections` (:497-530): `KnowledgePaths`, `Journeys`, `Visits` satırları `dash`; not `OutputsNoSource`.
  - L10n anahtarları `_TemplateFormL10n.cshtml:23` (`Inputs`, `Outputs`, …); resx `Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.*.resx` (7 dil).
- **Geri iz artık var:**
  - `KnowledgePath.ChainRef {ConceptChainTemplateId, ChainVersion}` (KP-1; `Domain/Entities/KnowledgePath.cs`), `CountryCode`, `LanguageCode`, `PathVersion`, `PathStatus`; legacy işaret `IsLegacyUnapproved` (Mapper / Dtos);
  - `KnowledgePathRevision` (KP-2): revizyon durumu / MLR sonucu;
  - "güncel yayın" tanımı `Features/Knowledge/Path/Release/KnowledgePathReleaseRules.cs` (`IsCurrentRelease`, `UsesPath`) — **kullan, kopyalama**;
  - yolculuk aşaması `RecommendedKnowledgePathId` + `PathCode` + `PathVersionPinPolicy` (`latest-published` kod ile eşleşir) — kullanım okuması zaten `KnowledgePathReleaseRules.UsesPath` / KP-3 kullanım ucunda;
  - `PlannedVisit.ContentItems[].PathId` (SB-3b).
- **Zincir şablonu sürümleri:** her sürüm ayrı kayıt (`ConceptChainTemplateId`), aynı `ChainCode`.

## NE
1. **CRM okuma ucu:** `GET /api/crm/knowledge/concept-chain-templates/{id}/outputs` (tenant'lı, salt okuma).
   - **Yollar:** `ChainRef.ConceptChainTemplateId == id` olan, arşivsiz yollar → `{pathId, pathCode, pathName, countryCode, languageCode, pathVersion, pathStatus, isCurrentRelease, latestRevisionStatus?, latestRevisionNumber?}`; sıralama ülke, dil, sürüm.
   - `?includeOtherVersions=true` ise aynı `ChainCode`'un diğer şablon sürümlerine bağlı yollar da (`chainVersion` alanıyla). Varsayılan: yalnız bu sürüm.
   - **Yolculuklar:** bu yolları (pinned: id; latest-published: kod) aşamalarında kullanan yolculuklar → `{journeyId, journeyCode, journeyName, journeyStatus, languageCode, stageCount (bu yolları kullanan aşama sayısı)}`. Kullanım kuralı KP-3'ün `UsesPath`'i.
   - **Ziyaretler:** `ContentItems[].PathId` bu yollardan biri olan, iptal / arşiv olmayan ve tarihi bugün ya da sonra olan planlı ziyaret **sayısı** (liste değil; kişisel veri taşımaz).
   - Toplamlar: `pathCount`, `currentReleaseCount`, `journeyCount`, `plannedVisitCount`.
   - **Yetki:** zincir şablonu okuma izni. Yol / yolculuk ayrıntısı için ilgili okuma izni yoksa o bölüm yalnız **sayı** döner + `restricted: true` (UAS: ayrıntı uydurulmaz, gizlenir).
2. **Web paneli** (`renderConnections`, "Çıktılar" bölümü):
   - üç satır gerçek sayılarla; "—" yalnız okunamadığında;
   - yollar listesi: kod — ad · ülke · dil · sürüm · durum rozeti (taslak / incelemede / onaylı / yayında / etkin dışı); `/CRM/KnowledgePaths/...` çalışma alanına bağlantı;
   - yolculuklar listesi: kod — ad · durum; Etkileşim Yolculukları sayfasına bağlantı;
   - ziyaret: "N planlı ziyaret" (bağlantısız);
   - "diğer sürümleri de göster" anahtarı;
   - kayıtlı olmayan (yeni) şablonda bölüm gizli ya da "kaydedince görünür";
   - `OutputsNoSource` notu kalkar.
3. **L10n:** yeni metinler 7 dilde (en, tr, fr, es, zh, ar, ru), TR diakritik; `_TemplateFormL10n` dizisi + PascalCase köprü.

## KORU / YAPMA
- **Yazma yok.** Zincir şablonu, yol, yolculuk, ziyaret verisi değişmez. Yeni koleksiyon / index yok.
- Zincir uyumu (BE-A / BE-B), sürüm akışları, sol / orta panel DEĞİŞMEZ.
- Kişisel veri (doktor adı vb.) dönmez; ziyaret yalnız sayı.
- Kullanıcı girdisi `esc()` ile.
- **DUR:** KP-3'teki "kullanım" kuralı (`UsesPath`) yolculuk için tek anlamlı değilse ya da ChainRef'siz yollar zincir şablonuyla başka bir yoldan ilişkilendiriliyorsa → raporla.

## Acceptance
- **E2:** CRM 0 kırmızı (taban **2153/0/5**), Web 0 kırmızı (taban **422/0**), build 0 hata.
  - **Yeni testler:** yalnız bu zincir sürümüne bağlı yollar (başka zincir / ChainRef'siz / arşiv dönmez); `includeOtherVersions`; tenant izolasyonu; `isCurrentRelease`; yolculuk kullanımı pinned + latest-published; planlı ziyaret sayısı (iptal / geçmiş / başka yol sayılmaz); yetkisizde sayı + `restricted`; Web panel sayıları / listeler / "—" yalnız hata; L10n 7 dil.
  - **Sabotaj:** (1) ChainRef filtresi kaldırılınca yol testi kırmızı; (2) ziyaret sayımında iptal filtresi kaldırılınca sayım testi kırmızı.
- **E4 (CT, Faz 0 sonrası):** TPL-ALMIBA-01 panelinde ALMIBA TR yolu + yolculuğu görünür.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-CH-1 (eski SB-2b) · Zincir şablonu "Çıktılar" paneli: bu zincirden kurulan Bilgi Yolları, kullanan yolculuklar, planlanan ziyaret sayısı
Repository: C:\tmp\kp-ch-1 (worktree) · Branch: wp/kp-ch-1 · commit bu dala, push YOK · CrmService (tek okuma ucu) + frontend/Diten.Web (panel)

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-KP-CH-1-chain-outputs-panel.md — önce tamamını oku (Kanıt / NE / KORU / Acceptance). Ayrıca: …/ROADMAP-content-to-visit.md (Faz 1) · …/DESIGN-KP-STUDIO-knowledge-path-studio.md §2.1 · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{KnowledgePath, KnowledgePathRevision, ContentEngagementJourney, PlannedVisit, ConceptChainTemplate}.cs · Application/Features/Knowledge/Path/Release/** (IsCurrentRelease, UsesPath — kullan, kopyalama) · Application/Features/Knowledge/Concept/** (chain template uçları) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (renderConnections) · Views/CRM/KnowledgeConcepts/_TemplateFormL10n.cshtml · Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.*.resx · memory l10n-bridge-pascalcase-loader, proxy-forward-204-content-length-crash.

NE: (1) CRM GET /api/crm/knowledge/concept-chain-templates/{id}/outputs — tenant'lı salt okuma: ChainRef'i bu şablon olan arşivsiz yollar {pathId, pathCode, pathName, countryCode, languageCode, pathVersion, pathStatus, isCurrentRelease, latestRevisionStatus?, latestRevisionNumber?}; ?includeOtherVersions=true → aynı ChainCode'un diğer sürümleri (chainVersion ile); bu yolları kullanan yolculuklar (UsesPath; pinned + latest-published) {journeyId, journeyCode, journeyName, journeyStatus, languageCode, stageCount}; ContentItems[].PathId bu yollardan olan, iptal/arşiv olmayan, bugün ve sonrası planlı ziyaret SAYISI; toplamlar. Yetki: zincir okuma; yol/yolculuk okuma izni yoksa o bölüm yalnız sayı + restricted:true. (2) Web "Çıktılar": gerçek sayılar, yol listesi (durum rozeti + çalışma alanı bağlantısı), yolculuk listesi (bağlantı), "N planlı ziyaret", diğer sürümler anahtarı, yeni şablonda gizli, OutputsNoSource notu kalkar; "—" yalnız okunamadığında. (3) L10n 7 dil + _TemplateFormL10n + PascalCase köprü.
KORU/YAPMA: yazma YOK; yeni koleksiyon/index yok; zincir uyumu/sürüm akışı/diğer paneller DEĞİŞMEZ; kişisel veri dönmez; esc().
DOĞRULA (E2): cd C:\tmp\kp-ch-1; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2153/0/5; bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 422); build 0 hata. Yeni testler WP Acceptance listesi. Sabotaj: (1) ChainRef filtresi kaldır → yol testi kırmızı; (2) ziyaret sayımında iptal filtresi kaldır → sayım testi kırmızı. Commit ("feat(crm): WP-KP-CH-1 — chain template outputs panel (knowledge paths, journeys, planned visits)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: UsesPath yolculuk için tek anlamlı değilse ya da ChainRef dışında bir zincir↔yol ilişkisi varsa → DUR + raporla.
```
