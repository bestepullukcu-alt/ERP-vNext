# WORK PACKAGE — WP-SEG-D · Sample/member ikincil etiket (GUID yerine specialty/type)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`1c08f32f` üstü). **Backend (CrmService) + küçük frontend.** Owner: *"sample members'ta isim altında id (GUID) geliyor; specialty/account bilgisi ise düzgün gelmeli."* SEG-C'nin bilinçli açık noktası (sampleMembers yalnız displayName; specialty/workplace zenginleştirme = "opsiyonel ayrı WP, resolver member-projection") — işte o WP.

## Ölçülmüş girdi (CT)
- **`SegmentSubjectSnapshot`** (Resolution) ZATEN taşıyor (ekstra read YOK, aynı candidate projection): `DisplayName, Type, Category, Status, Country, City, District, ParentAccountId, Specialty, ProfessionalTitle, Department, Gender, PreferredLanguage`.
- **Preview handler** (`PreviewSegmentReachHandler.cs:134-135`): `sampleMembers = full.Members.Select(m => new SegmentReachSampleMemberDto(m.SubjectId, m.SubjectType, m.SubjectDisplayName))` — ikincil etiket taşımıyor.
- **Frontend** (`form.js:842-849`): `meta = m.displayName ? (m.subjectId || '') : ''` → **displayName varken meta olarak ham GUID (subjectId)** basıyor. (Aynı sorun manuel üye listesinde `form.js:1123` `seg-member-meta = m.subjectId`.)
- **Member DTO** (`SegmentMemberResultDto`): SubjectId + SubjectType + SubjectDisplayName; `/resolve` yanıtı da bunu kullanıyor.

## Kapsam
**1) Backend — snapshot'tan ikincil etiket (ekstra read YOK):**
- `SegmentSubjectSnapshot`'tan **`SubjectSecondaryLabel`** türet: **contact →** `Specialty` (yoksa `ProfessionalTitle`, yoksa `City`); **account →** `Type` (yoksa `Category`, yoksa `City`). Varsa `City` ile birleştir: `"Nephrology · İstanbul"` / `"University hospital · Ankara"`. Hepsi boşsa `null`.
- `SegmentMemberResultDto`'ya **additive nullable `SubjectSecondaryLabel`** ekle; resolver member'ı üretirken (candidate snapshot elde) doldur. **`/resolve` şekli additive genişler (nullable), mevcut alanlar/sıra/anlam DEĞİŞMEZ.**
- `SegmentReachSampleMemberDto`'ya **additive `SubjectSecondaryLabel`** ekle; preview handler member'dan kopyalasın.

**2) Frontend (form.js, minimal):**
- Sample render (842-849): `meta = m.subjectSecondaryLabel || ''` (GUID fallback KALDIR — subjectId asla meta değil).
- Manuel üye listesi (1123): `seg-member-meta = m.subjectSecondaryLabel || ''` (GUID gösterme; isim yoksa subjectId ada düşebilir ama meta'da ham GUID yok).

## KORU / YAPMA
- `/resolve` member SET/ORDER/count/reason DEĞİŞMEZ (yalnız additive nullable alan). Resolution logic, candidate source, pushdown, cap, PII gate DEĞİŞMEZ. Ekstra Mongo read EKLEME (snapshot zaten taşıyor). Payload/catalog/reach (SEG-A/B/C) + same-origin proxy DEĞİŞMEZ. Frontend'de yalnız meta 2 satır — blok editör/buildNodes/reach DOKUNMA. Yeni CSS yok. Başka modül.

## Acceptance
- **E2:** CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail. Preview sampleMembers + `/resolve` member `SubjectSecondaryLabel` taşır (contact→specialty, account→type; +city varsa); ekstra read yok (snapshot reuse); `/resolve` mevcut alanları değişmedi (additive). Frontend sample/member meta = ikincil etiket; **ham GUID meta'da görünmüyor**. Boş etiket → meta gizli.
- **E4:** ALMIBA nefrolog preview → sample isim altında "Nephrology" (GUID değil); account segmenti → "University hospital · İstanbul".

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-D · Sample/member ikincil etiket (GUID yerine specialty/type) (MOD-0167-FU02, backend+küçük frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (1c08f32f üstü)> · Worktree: ana checkout

Önce oku: WP-SEG-D-sample-member-secondary-label.md · Features/Segmentation/Resolution/SegmentSubjectSnapshot.cs (Specialty/Type/City ZATEN var) + SegmentMembershipResolver.cs (member üretimi) + Handlers/QueryHandlers/PreviewSegmentReachHandler.cs (satır 134) + Segmentation SegmentModels.cs (SegmentMemberResultDto + SegmentReachSampleMemberDto) + frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (satır 842-849 sample, 1123 manuel üye).

NE:
 Backend (ekstra read YOK — snapshot reuse):
 - SegmentSubjectSnapshot'tan SubjectSecondaryLabel türet: contact→Specialty(yoksa ProfessionalTitle, yoksa City); account→Type(yoksa Category, yoksa City); City varsa "X · City" birleştir; hepsi boşsa null.
 - SegmentMemberResultDto'ya additive nullable SubjectSecondaryLabel ekle; resolver member üretirken (snapshot elde) doldur. /resolve şekli additive (nullable), mevcut alanlar/sıra/anlam DEĞİŞMEZ.
 - SegmentReachSampleMemberDto'ya additive SubjectSecondaryLabel; preview handler member'dan kopyala.
 Frontend (form.js minimal):
 - sample (842-849): meta = m.subjectSecondaryLabel || '' (subjectId GUID fallback KALDIR).
 - manuel üye (1123): seg-member-meta = m.subjectSecondaryLabel || '' (ham GUID gösterme).
KORU/YAPMA: /resolve member SET/ORDER/count/reason değişmez (yalnız additive nullable); resolution logic/candidate source/pushdown/cap/PII gate değişmez; EKSTRA Mongo read EKLEME (snapshot zaten taşıyor); payload/catalog/reach + same-origin proxy değişmez; frontend'de yalnız meta 2 satır (buildNodes/reach/blok DOKUNMA); yeni CSS yok; başka modül.
DOĞRULA (E2): TAM CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail; preview sample + /resolve member SubjectSecondaryLabel taşır (contact→specialty/account→type, +city); ekstra read yok; /resolve additive (mevcut alanlar değişmedi); frontend meta=ikincil etiket, ham GUID meta'da yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: snapshot secondary label türetilemiyorsa; /resolve additive tutulamıyorsa (şekil bozuluyorsa DUR); ekstra read gerekiyorsa (DUR+raporla); kapsam Segmentation+form.js meta dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 86b4232f · Agent: PASS (--no-build) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-segd-verify @86b4232f
```
- ✅ **Scope:** 6 dosya (WP md + form.js + 4 Segmentation backend: Snapshot/Resolver/PreviewHandler/SegmentModels). Başka modül/DTO sızıntısı yok.
- ✅ **/resolve additive:** `SegmentMemberDto` + `SegmentReachSampleMemberDto`'ya `string? SubjectSecondaryLabel = null` **en sonda nullable** — mevcut alan/sıra/anlam değişmedi (git diff onaylı).
- ✅ **Ekstra read YOK:** `SegmentSubjectSnapshot.SecondaryLabel` **computed property** — contact→Specialty(yoksa ProfessionalTitle), account→Type(yoksa Category), +City "core · city"; aynı candidate projection alanlarından türer, yeni Mongo read/round-trip yok. Salt görüntü (kural değerlendirmesine girmez).
- ✅ **Resolver:** member+excluded+hybrid-promote yollarında snapshot'tan doldurulur; manuel/statik üyede snapshot yok → null (doğru). Member SET/ORDER/count/reason değişmedi.
- ✅ **Frontend:** sample meta = `subjectSecondaryLabel` (GUID fallback kalktı); manuel üye ham `subjectId` satırı → secondaryLabel (boşsa gizli). Yeni CSS yok; buildNodes/reach/blok dokunulmadı.
- ✅ **Build+test (CT izole, Release, GERÇEK build):** CrmService.Application.Tests **1740/0/5** + Diten.Web.Tests **137/0** (agent fleet DLL-kilidi yüzünden --no-build koşmuştu; CT gerçek rebuild ile teyit).
- ⏳ **E4:** ALMIBA nefrolog preview sample → "Nephrology" (GUID değil); account → "University hospital · İstanbul".

**SEG-C açık noktası kapandı: sample/üye artık specialty/type gösterir, ham GUID yok.**
