# WORK PACKAGE — WP-SEG-DETAILS7 · Criteria human cümle (Details, frontend-only)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`57b8a0b9` üstü). **Yalnız frontend (Details.cshtml + gerekirse resx).** Owner: criteria "account.type" (kod) yerine "Account type is hospital" / "Specialty is Nephrology" (mockup human cümle). Owner kararı: **ayrı ayrı, önce human label** (görünür kazanım). Territory node **ismi + edit cascade** ayrı WP (MOD-0151 lookup — WP-DETAILS8).

## Ölçülmüş girdi (CT)
- Details criteria server-render (Details.cshtml ~201): `segd-pred-human` = `node.Label` varsa onu, yoksa `node.AttributeCode` ("account.type").
- `node.Label` = kullanıcının ELLE girdiği (SegmentMapper trim); otomatik human üretimi YOK.
- operator iş-dili (`in`→"is any of") **frontend L10n'de** (form.js `OPERATOR_LABEL_KEY`); attribute/operator display backend'de YOK.
- reference-set/enum değerler okunur (`nephrology`/`hospital`); entity-picker (territory node) değeri GUID (DETAILS2'de badge'de zaten Guid.TryParse ile gizli).

## Kapsam (Details.cshtml + resx)
`segd-pred-human` boş-Label durumunda **otomatik human cümle** kur (Razor, backend/katalog yok):
- **Attribute display:** `AttributeCode` humanize — son segment + kebab→boşluk + Title Case: `contact.specialty`→"Specialty", `account.type`→"Account type", `consent.eligibility`→"Contactability" (mümkünse iş-dili eşleme; değilse humanize).
- **Operator phrase:** `node.Operator`→iş-dili (`in`="is any of", `not-in`="is none of", `eq`="is", `gt`="is after", `lt`="is before", `gte`="is at least", `lte`="is at most"). form.js `OPERATOR_LABEL_KEY` ile **aynı resx anahtarlarını** Razor `Localizer` ile kullan (mümkünse yeni anahtar ekleme; yoksa 7 dil ekle).
- **Value display:** reference-set/enum değerler (Nephrology, hospital — Title Case); **36-char GUID/UUID değerler ATLA** (territory node — WP-DETAILS8'de isim gelecek; şimdilik "Attribute is any of" value'suz veya value gizli). Çoklu değer virgülle.
- Sonuç: "Specialty is any of Nephrology", "Account type is any of Hospital", "Contactability is Eligible". Label kullanıcı girmişse onu KORU (elle label > otomatik).

## KORU / YAPMA
- Attribute/operator/value **badge'leri** (segd-tag-attr/op mono) KALIR (DETAILS2'deki GUID-gizleme davranışı korunur). node.Label (elle) öncelik. Backend/DTO/katalog DOKUNMA (frontend-only). Resolution/preview/lifecycle/scope/tema/app-card/details.js DEĞİŞMEZ. Uydurma değer YOK (GUID atla, isim uydurma). segment-create.css DOKUNMA. Territory node ISMI + edit cascade bu WP'de DEĞİL (WP-DETAILS8). Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). Criteria predicate human cümle ("Specialty is any of Nephrology"); elle node.Label varsa korunur; GUID value atlanır (uydurma yok); badge'ler (attr/op mono) korunur. git diff yalnız Details.cshtml(/resx).
- **E4:** criteria "WHERE Specialty is any of Nephrology / AND Account type is any of Hospital" (kod değil).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-DETAILS7 · Criteria human cümle (Details, frontend-only) (MOD-0167-FU02)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (57b8a0b9 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS7-criteria-human-label.md · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml (criteria predicate ~198-215: segd-pred-human, node.Label/AttributeCode/Operator/Values) · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (OPERATOR_LABEL_KEY + operatorLabel — operator resx anahtarları) · frontend/Diten.Web/Resources/Views/CRM/Segments/SegmentsIndex.*.resx (mevcut operator anahtarları).

NE (Details.cshtml + gerekirse resx):
 - segd-pred-human: node.Label boşsa otomatik human cümle kur (Razor @functions helper): humanize(AttributeCode) [son segment + kebab→boşluk + Title Case; contact.specialty→Specialty, account.type→Account type] + operatorPhrase(node.Operator) [in=is any of, not-in=is none of, eq=is, gt=is after, lt=is before, gte=is at least, lte=is at most — form.js OPERATOR_LABEL_KEY ile aynı resx anahtarları, Localizer; yoksa 7 dil ekle] + valueDisplay [reference-set/enum Title Case; 36-char GUID/UUID ATLA (territory node — WP-8), çoklu virgülle]. node.Label elle girilmişse KORU (öncelik).
KORU/YAPMA: attr/op/value badge (segd-tag mono) KALIR + DETAILS2 GUID-gizleme korunur; backend/DTO/katalog DOKUNMA (frontend-only); resolution/preview/lifecycle/scope/tema/app-card/details.js DEĞİŞMEZ; GUID value atla (uydurma isim yok); segment-create.css DOKUNMA; territory node ismi/edit-cascade bu WP'de YOK; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); criteria human cümle (Specialty is any of Nephrology / Account type is any of Hospital); elle Label korunur; GUID value atlanır; badge korunur; git diff yalnız Details.cshtml(/resx). Ayrı commit. §22 TÜRKÇE. K13.
Durma: operator resx anahtarı yoksa 7 dil eklenemiyorsa; humanize belirsizse; backend gerekiyorsa DUR; kapsam Details.cshtml/resx dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests baseline-diff; criteria human cümle (humanize+operator phrase+value; GUID atlanır); elle node.Label korunur; badge korunur; frontend-only (backend dokunulmadı); git diff Details.cshtml(/resx).

## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 4ed04850 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-det7-verify @4ed04850
```
- ✅ Scope: yalnız Details.cshtml (+73/-1). Backend/js/css/resx sızıntı=0 (mevcut 12 operator anahtarı reuse, yeni resx yok).
- ✅ human cümle: @functions helper (OperatorLabelKey form.js OPERATOR_LABEL_KEY ile birebir + HumanizeAttribute + IsGuidValue + TitleCaseValue); node.Label boşsa "humanize(attr) + operator phrase + value"; elle node.Label öncelik (korunur); 36-char GUID value atlanır (uydurma yok, territory node ismi WP-DETAILS8'e); iş-dili eşleme account.type→"Account type", consent.eligibility→"Contactability".
- ✅ Korundu: attr/op/value mono badge + DETAILS2 GUID-gizleme; backend/DTO/katalog/resolution/preview/details.js/segment-create.css dokunulmadı.
- ✅ Build+test (CT izole, Release, GERÇEK): Diten.Web.Tests 137/0.
- ⏳ E4: criteria "WHERE Specialty is any of Nephrology / AND Account type is any of Hospital".
