# WORK PACKAGE — WP-CT-L10N-LEGACY · Zincir Şablonu editörünün v2-öncesi 24 anahtarını 5 dile çevir (frontend, resx-only)

> **CT (SoR).** MOD-0162 Chain Template v2 — FE-7 yerine. Branch `feature/crm-chain-template` (FE-3-REFINE `170b21fb` / §37 `c1a0bb15` üstü). **CT L10n denetimi (2026-09-26):** 7 dilde anahtar eşliği, köprü, JS kapsaması, `{0}` yer tutucuları ve v2'de eklenen tüm yeni anahtarlar **temiz**; kaydetme payload'ı BE-B sözleşmesiyle uyumlu. **Tek boşluk:** editörün **v2-öncesi (SCMM-10 dönemi) 24 anahtarı** fr/es/zh/ar/ru'da **İngilizce kalmış** — ekranda görünüyor (başlık, kimlik alanları, adım kontrolleri, mesajlar). 7 dil kuralı gereği iş bitmemiş sayılır. **Yalnız resx `<value>`**; kod/köprü/JS/backend DEĞİŞMEZ.

## Kanıt (CT denetimi)
- Dosyalar: `frontend/Diten.Web/Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{en,tr,fr,es,zh,ar,ru}.resx` — 7 dilde **266 anahtar**, eşit set, tekrar yok, boş değer yok.
- Köprü `_TemplateFormL10n.cshtml` (104 anahtar) + `_IndexL10n.cshtml` (163) → hepsi 7 resx'te var. `template-form.js` 69 / `concept-slim.js` 42 anahtar → hepsi köprüde.
- **tr:** eksik 0. **es/zh/ar/ru:** aşağıdaki 24 anahtar değeri = en (çevrilmemiş). **fr:** aynı liste; ancak `Description` Fransızcada meşru aynı yazılır (dokunma).
- Fransızcada en ile aynı yazılan **meşru** (DOKUNMA): `Description`, `Branches`, `BranchCount`, `TabVersions`(Versions), `TypePalette`(Types), `ForWhom`(Audience), `MinSelection`(Min), `MaxSelection`(Max).

## Çevrilecek 24 anahtar (en değeri)
| Anahtar | en |
|---|---|
| CreateTemplate | Create Chain Template |
| EditTemplate | Edit Chain Template |
| ChainCode | Chain Code |
| ChainName | Chain Name |
| ChainVersion | Chain Version |
| SubjectId | Subject |
| Status | Status |
| Description | Description *(fr hariç)* |
| EffectiveFrom | Effective From |
| EffectiveTo | Effective To |
| Edit | Edit |
| AddToSequence | Add step |
| MoveUp | Move up |
| MoveDown | Move down |
| RemoveStep | Remove step |
| SelectOption | Select... |
| Loading | Loading... |
| ErrorState | Something went wrong. |
| RecordCreated | Record created. |
| RecordUpdated | Record updated. |
| SequenceMinTwo | The sequence needs at least two concept types. |
| PublishedSequenceFrozen | A published chain freezes its sequence; a change needs a new version. |
| ImmutableAfterCreate | Fixed at creation; it cannot be changed later. |
| NodePickerSubjectFirst | Pick a subject first; the node lists are scoped to it. |

## Terim tutarlılığı (ZORUNLU)
- **Aynı resx'te v2 anahtarlarının zaten kullandığı terimleri** kullan (ör. dal/omurga/adım/tip için `BranchLabelPrefix`, `Spine`, `StepCountLabel`, `TypePalette`, `AddTypeCompact`, `Publish*` değerlerine bak) — aynı kavram iki farklı kelimeyle çevrilmesin.
- **Subject** = WP-KNOWLEDGE-L10N sözlüğü: FR *Sujet* · ES *Tema* · ZH *主题* · AR *الموضوع* · RU *Тема*.
- **Chain Template** (öneri, dosyada mevcut kullanım varsa ona uy): FR *Modèle de chaîne* · ES *Plantilla de cadena* · ZH *链模板* · AR *قالب السلسلة* · RU *Шаблон цепочки*.
- **Concept type:** FR *type de concept* · ES *tipo de concepto* · ZH *概念类型* · AR *نوع المفهوم* · RU *тип концепта*.
- Noktalama/üç nokta biçimi en ile aynı (`Loading...`, `Select...`).

## NE (yalnız resx `<value>`)
1. `KnowledgeConceptsIndex.{fr,es,zh,ar,ru}.resx` içinde yukarıdaki 24 anahtarın **değerini** o dile çevir (fr'de `Description` hariç 23).
2. Not: `Status/Description/Edit/Loading/SelectOption/ErrorState/RecordCreated/RecordUpdated` **konsolla paylaşılan** anahtarlar — çevirmek konsolun bu metinlerini de düzeltir (istenen, yan fayda).

## KORU / YAPMA
- **Yalnız bu 5 resx dosyasının, yalnız bu 24 anahtarının `<value>`'su.** `en` ve `tr` DOKUNMA. Anahtar ekleme/silme/yeniden adlandırma YOK (266 sabit), sıra/`xml:space`/CRLF korunur. Köprü (`_TemplateFormL10n`, `_IndexL10n`), JS, cshtml, backend DEĞİŞMEZ. Değer anahtar adını echo'lamaz. Listede olmayan diğer İngilizce-kalmış konsol anahtarları (Save/Cancel/Actions/Active…) **bu WP'de YOK** (ayrı borç: F-CONCEPTS-L10N-FULL).
- **DUR:** bir terimin dosyada iki farklı çevirisi varsa (tutarsız mevcut kullanım) → çoğunluğu seç + raporla.

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → **229/0**. git diff: yalnız 5 resx, ~119 `<value>` satırı; en/tr/kod diff YOK; anahtar sayısı 266 (7 dilde).
- **CT denetim tekrarı:** fr/es/zh/ar/ru'da bu 24 anahtarda "en ile aynı" kalan = 0 (fr'de yalnız meşru `Description`).
- **E4 (FLEET RESTART):** arayüz dilini ES/ZH/AR/RU/FR'ye alıp Zincir Şablonu editörünü aç → başlık, kimlik alanları, adım kontrolleri, uyarı mesajları o dilde.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CT-L10N-LEGACY · Zincir Şablonu editörünün v2-öncesi 24 anahtarını 5 dile çevir (MOD-0162, frontend, resx-only)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-chain-template · Worktree: ana checkout

CT L10n denetimi: 7 dil eşliği/köprü/JS/yer tutucu ve v2 yeni anahtarları temiz; tek boşluk editörün v2-öncesi 24 anahtarı fr/es/zh/ar/ru'da İngilizce kalmış (ekranda görünüyor). YALNIZ resx <value>; en/tr/kod/köprü/backend DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CT-L10N-LEGACY-editor-keys.md (24 anahtar tablosu + terim sözlüğü) · frontend/Diten.Web/Resources/Views/CRM/KnowledgeConcepts/KnowledgeConceptsIndex.{en,fr,es,zh,ar,ru}.resx (v2 anahtarlarının mevcut terimleri: BranchLabelPrefix, Spine, StepCountLabel, TypePalette, AddTypeCompact, Publish*).

NE:
 1) fr/es/zh/ar/ru resx'te şu 24 anahtarın <value>'sunu çevir: CreateTemplate, EditTemplate, ChainCode, ChainName, ChainVersion, SubjectId, Status, Description (fr HARİÇ — meşru), EffectiveFrom, EffectiveTo, Edit, AddToSequence, MoveUp, MoveDown, RemoveStep, SelectOption, Loading, ErrorState, RecordCreated, RecordUpdated, SequenceMinTwo, PublishedSequenceFrozen, ImmutableAfterCreate, NodePickerSubjectFirst.
 2) Terim tutarlılığı: aynı resx'te v2 anahtarlarının kullandığı terimlerle hizala; Subject = FR Sujet / ES Tema / ZH 主题 / AR الموضوع / RU Тема; Chain Template = FR Modèle de chaîne / ES Plantilla de cadena / ZH 链模板 / AR قالب السلسلة / RU Шаблон цепочки (dosyada mevcut kullanım varsa ona uy); Concept type = FR type de concept / ES tipo de concepto / ZH 概念类型 / AR نوع المفهوم / RU тип концепта. Üç nokta biçimi en ile aynı.
 3) fr'de meşru aynı yazılanlara DOKUNMA: Description, Branches, BranchCount, TabVersions, TypePalette, ForWhom, MinSelection, MaxSelection.
KORU/YAPMA: yalnız 5 resx × bu 24 anahtarın <value>'su; en/tr DOKUNMA; anahtar ekleme/silme/yeniden adlandırma YOK (266 sabit); sıra/xml:space/CRLF korunur; köprü/JS/cshtml/backend DEĞİŞMEZ; değer anahtarı echo'lamaz; diğer İngilizce-kalmış konsol anahtarları (Save/Cancel/Actions/Active…) bu WP'de YOK.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 229/0; git diff yalnız 5 resx <value> satırları; en/tr/kod diff yok; 7 dilde anahtar sayısı 266; bu 24 anahtarda fr/es/zh/ar/ru değeri en ile aynı kalan yok (fr Description hariç). Ayrı commit ("i18n(crm): WP-CT-L10N-LEGACY — translate 24 pre-v2 chain template editor keys into fr/es/zh/ar/ru (MOD-0162)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: bir terimin dosyada iki farklı mevcut çevirisi varsa çoğunluğu seç + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-27) → **ACCEPTED (E2)**
```
Commit: 22182eed · Agent: PASS (229/0) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-l10nleg @22182eed → Diten.Web.Tests 229/0
```
- ✅ **Kapsam (5 dosya, +119/−119):** yalnız fr/es/zh/ar/ru resx; değişen satırların **tamamı `<value>`** (value-dışı değişen satır = 0). en/tr/köprü/JS/cshtml/backend diff YOK.
- ✅ **Anahtar seti:** 7 dilde 266 (eşit). CRLF korunmuş (ru 273/273 satır CRLF).
- ✅ **Denetim tekrarı:** 24 anahtarda en ile aynı kalan → fr yalnız `Description` (meşru); es/zh/ar/ru **0**.
- ✅ **Terim hizası:** Sequence → v2 `Spine` değeriyle aynı (Ossature/Columna/主干/العمود الفقري/Каркас); Chain Template/Subject sözlükle aynı.
- ⚠ **Raporlanan ayrışma (kabul):** RU "kavram" dosyada понятие ×7 / концепт ×1 (`ConceptType`="Тип концепта") → çoğunluk seçildi (`SequenceMinTwo` "типов понятий"). `ConceptType` kapsam dışı; tek terime birleştirme → **F-CONCEPTS-L10N-FULL**.

**WP-CT-L10N-LEGACY KOMPLE (E2).** Chain Template v2 7 dilde tamam (BE-A/B + FE-1..6 + FE-3-REFINE + L10N-LEGACY hepsi CT-E2). Kalan: **canlı E4 (fleet restart)** + push `feature/crm-chain-template` + PR.
