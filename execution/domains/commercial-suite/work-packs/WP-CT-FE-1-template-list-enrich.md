# WORK PACKAGE — WP-CT-FE-1 · Zincir Şablonu listesi zenginleştir (concept console tab-4) (frontend, mockup v2)

> **CT (SoR).** MOD-0162 Concept Graph / Chain Template. Branch `feature/crm-chain-template`. Zincir Şablonu v2 mockup **Ekran 1 (liste)**. **Kullanıcı kararı:** ayrı sayfa değil — **mevcut concept console tab-4'ü (Zincir Şablonları) zenginleştir**; **konuya-özel kalır** (çapraz-konu değil). **Frontend-only** — liste DTO gerekli tüm alanları **zaten taşıyor** (backend DEĞİŞMEZ). BE-A/BE-B üstüne.

## Kanıt
- **Liste DTO tam** (`ConceptGraphMapper.cs:25-27`): `SubjectId, ChainCode, ChainName, Description, OrderedConceptTypes[], Branches[](ToBranchDtos), ModeratorRoleType, ForWhomAudienceProfileIds[], Status, ChainVersion, EffectiveFrom/To` → **backend enrichment gerekmez**.
- **Mevcut tab-4 kolonları** (`_TemplatesDataTable.cshtml:14-29`): Kod · Ad · **SubjectId (ham)** · OrderedConceptTypes · SequenceLength · ChainVersion · Status · EffectiveFrom/To · Archived · UpdatedAt · Actions.
- **Render + çözüm haritaları** (`concept-slim.js`): tab config `concept-chain-templates` (satır 201, idField `conceptChainTemplateId`, nameField `chainName`); **moderatorMap + audienceMap zaten yükleniyor** (satır 83/154, quick-view için). Concept **types** konu için yükleniyor (typeId→name çözümü mevcut/erişilir). Konu seçici üstte (seçili konu adı biliniyor).
- **Mockup Ekran 1 kolonları:** Konu (ad+kısa tanım) · Zincir adı · Kod · **Omurga (tip etiketleri, oklarla)** · **Dal (sayı)** · **Moderatör** · **Kime** · **Durum+sürüm** (taslak satırı "v2 · v1 yayında" da gösterir). Satır → editör.

## NE (frontend; backend/list-DTO DEĞİŞMEZ)
1. **Kolonları mockup'a getir** (`_TemplatesDataTable.cshtml` başlıklar + `concept-slim.js` columnDefs, tab-4):
   - **Konu** ← `subjectId` çöz → konu adı (konuya-özel görünümde seçili konu; çözümlenmiş göster). *(redundant ama mockup yapısı; istenirse ikincil satırda kısa kod/tanım.)*
   - **Zincir adı** ← `chainName` · **Kod** ← `chainCode` (kilit/donuk ima gerekmez listede).
   - **Omurga** ← `orderedConceptTypes[]` → **tip ADLARI** `→` ile birleştir (typeId→name çöz; boşsa "—"). Uzun ise sarma/rozet.
   - **Dal** ← `branches.length` (sayı rozeti).
   - **Moderatör** ← `labelModerator(moderatorRoleType)` (mevcut moderatorMap; boşsa "—").
   - **Kime** ← `forWhomAudienceProfileIds` → adet rozeti (veya audienceMap ile ilk N ad); boşsa "—".
   - **Durum + sürüm** ← `status` + `chainVersion` birleşik; **taslak satırında**, aynı `chainCode`'un **published** kardeşini (client-side, tüm satırlar yüklü) bul → "v{X} taslak · v{Y} yayında" ekle. Durum rozet tonu (taslak/yayında/arşiv).
   - **Satır tıklama → mevcut editör** (TemplateEdit rotası) korunur.
   - Gereksiz kolonlar (SequenceLength/EffectiveFrom/To/Archived/UpdatedAt) kalabilir veya colVis ile gizli varsayılan — mockup sadeliğine yaklaştır (silme, gizle).
2. **Çözüm haritaları:** typeId→name (konu tipleri; yoksa yükle) + subjectId→name (seçili konu adı). moderator/audience haritaları mevcut — reuse. Çözüm başarısızsa ham id yerine "—"/kod fallback (graceful).
3. **L10n (7 dil):** yeni/başlık anahtarları — `Spine`/"Omurga" (OrderedConceptTypes reuse edilebilir), `BranchCount`/"Dal", `Moderator`/"Moderatör" (var), `ForWhom`/"Kime" (var), `StatusVersion`/"Durum" birleşik başlık, `PublishedSiblingHint` ("v{0} yayında"). `_IndexL10n`/template L10n köprüsüne ekle.

## KORU / YAPMA
- **Backend / list DTO / diğer tab'lar (Types/Nodes/Relationships) / editör sayfası (TemplateCreate/Edit) DEĞİŞMEZ** (editör = FE-2+). Konuya-özel kalır (**çapraz-konu YAPMA**; ListAsync all-subject ÇAĞIRMA). DataTable Golden Slim deseni + mevcut filtre/colvis korunur. Tema/L10n köprüsü ([[l10n-bridge-pascalcase-loader]]). Çözüm graceful (id sızdırma yok). Konu seçici davranışı korunur.
- **DUR:** typeId→name için konu tipleri liste sayfasında erişilemiyorsa (ayrı fetch gerekiyorsa) additive ekle; Durum+sürüm published-kardeş eşleşmesi tüm satırlar yüklü değilse (server-side paging) → client-side eşleşme yerine yalnız kendi durumunu göster + raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → yeşil. git diff: `_TemplatesDataTable.cshtml` + `concept-slim.js` + resx (+ gerekirse concept L10n köprüsü). **backend/diğer tab/editör diff YOK.**
- **E4 (FLEET RESTART):** tab-4'te Konu (ad) · Omurga (tip adları oklu) · Dal (sayı) · Moderatör · Kime · Durum+sürüm (taslakta "· vX yayında") kolonları; satır → editör. TUTUKON konusunda zincir varsa doğru görünür.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-FE-1 · Zincir Şablonu listesi zenginleştir (concept console tab-4) (MOD-0162, frontend, mockup v2)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

Kullanıcı kararı: ayrı sayfa değil — mevcut concept console tab-4 (Zincir Şablonları) zenginleştir; konuya-özel kalır (çapraz-konu DEĞİL). Frontend-only; liste DTO tüm alanları zaten taşıyor (backend DEĞİŞMEZ).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-FE-1-template-list-enrich.md · frontend/Diten.Web/Views/CRM/KnowledgeConcepts/_TemplatesDataTable.cshtml (başlıklar 14-29) · frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/concept-slim.js (tab config 'concept-chain-templates' ~201, moderatorMap/audienceMap ~83/154, type çözümü) · _IndexL10n.cshtml + KnowledgeConceptsIndex resx (7 dil) · services/.../Concept/ConceptGraphMapper.cs:25-27 (liste DTO alanları — SALT referans) · memory l10n-bridge-pascalcase-loader.

NE (frontend; backend/list-DTO DEĞİŞMEZ):
 1) tab-4 kolonlarını mockup Ekran 1'e getir: Konu(subjectId→ad çöz) · Zincir adı(chainName) · Kod(chainCode) · Omurga(orderedConceptTypes→tip ADLARI '→' ile) · Dal(branches.length rozet) · Moderatör(labelModerator(moderatorRoleType)) · Kime(forWhomAudienceProfileIds adet/ad) · Durum+sürüm(status+chainVersion; TASLAK satırında aynı chainCode'un published kardeşini client-side bul → "vX taslak · vY yayında"). Satır → mevcut TemplateEdit rotası. Gereksiz kolonları (SequenceLength/Effective*/Archived/UpdatedAt) colVis-gizli varsayılan yap (silme).
 2) Çözüm: typeId→name (konu tipleri; yoksa additive yükle) + subjectId→name; moderator/audience haritaları reuse; graceful fallback ("—"/kod, id sızdırma yok).
 3) L10n 7 dil yeni başlıklar (Dal/Durum-birleşik/PublishedSiblingHint "vX yayında"; Omurga/Moderatör/Kime reuse) + köprü whitelist.
KORU/YAPMA: backend/list-DTO/diğer tab(Types/Nodes/Relationships)/editör(TemplateCreate/Edit) DEĞİŞMEZ; konuya-özel kal (çapraz-konu/ListAsync-all ÇAĞIRMA); Golden Slim filtre/colvis korunur; tema/L10n köprüsü; çözüm graceful.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → yeşil; git diff _TemplatesDataTable.cshtml + concept-slim.js + resx(+köprü); backend/diğer tab/editör diff yok. Ayrı commit ("feat(crm): WP-CT-FE-1 — enrich chain template list (subject/spine names, branch count, moderator, forwhom, status+version) (MOD-0162)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: typeId→name liste sayfasında erişilemiyorsa additive ekle; published-kardeş eşleşmesi server-side paging yüzünden imkansızsa yalnız kendi durumunu göster + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-24) → **ACCEPTED (E2)**
```
Commit: 1cb3f6cb · Agent: PASS (229/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-fe1 @1cb3f6cb → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (10 dosya, +135/−32):** 7 resx (+4 anahtar) · _IndexL10n köprü · _TemplatesDataTable · concept-slim.js. **backend/editör(TemplateCreate/Edit)/diğer tab(Types/Nodes/Relationships) diff YOK.** Frontend-only (liste DTO değişmedi).
- ✅ **Render (kod okundu):** Omurga = tip adları ok-ikonlu birleşik, çözülemeyen → **"—" (ham id sızmaz)** · Dal = branches.length rozet · Kime = ilk ad +N · **Durum+sürüm:** taslak/arşiv-dışı satırda aynı subject+chainCode+published kardeşi bul → "vY yayında" hint · satır→TemplateEdit (arşivli=link yok) · client-side (tüm satır yüklü, published-sibling eşleşmesi mümkün — DUR tetiklenmedi).
- ✅ **Detay kolonları** (SequenceLength/Effective/Archived/Updated) silinmedi → **colVis-gizli varsayılan**; yeni pageKey `…ChainTemplatesV2` (eski kayıtlı görünüm bir kez sıfırlanır — kabul).
- ✅ **L10n 7 dil:** 4 yeni anahtar (Spine/BranchCount/StatusVersion/PublishedSiblingHint) + köprü; Konu/Moderatör/Kime mevcut anahtarları reuse.
- ✅ **Build+test:** Diten.Web.Tests **229/0**. (Agent canlı ekrana giremedi → prod concept-slim.js'i stub fetch/DataTable ile koşup render doğruladı.)

**WP-CT-FE-1 KOMPLE (E2).** E4 = fleet restart (resx başlıkları) → tab-4 zengin kolonlar. Sıradaki: FE-2 (3-sütun editör iskeleti + tip paleti).

