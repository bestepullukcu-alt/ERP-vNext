# WORK PACKAGE — WP-SEG-F · Sample ikincil etikete workplace (bağlı account adı) ekle (backend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`d63befa5` üstü). **Backend (CrmService).** Owner: sample members'ta contact'ın **bağlı olduğu account'ı** (workplace) da göster — "Dr. Ayşe Yılmaz / Nephrology · Marmara Univ. Hospital" gibi. SEG-D contact için `Specialty · City` veriyordu; owner `Specialty · Workplace(account adı)` istiyor.

## Ölçülmüş girdi (CT)
- `SegmentCandidateSource` (Persistence) contact→account link'i ZATEN bulk okuyor (`LoadLinksAsync`, Phase-1.5): `SegmentLinkProjection(ContactId, AccountId, RoleCode, IsPrimary, AccountType)`; satır ~141-157'de linked account'lar için **bulk read** yapılıyor (`accounts` koleksiyonu, `Project(Include("AccountType"))`). **Account `Name`'i o AYNI bulk read'e eklemek ekstra sorgu DEĞİL** (bir projeksiyon alanı).
- `SegmentSubjectSnapshot`: contact `Specialty/City…` var; **primary account adı (workplace) YOK**. `SecondaryLabel` (SEG-D) contact → `Specialty(·City)`.

## Kapsam (backend, minimal)
1. **`SegmentLinkProjection`'a additive `AccountName`** ekle; `SegmentCandidateSource` linked-account bulk read'inde `Project(Include("AccountType","Name"))` (aynı sorgu, +1 alan). Primary link (`IsPrimary`) account adı taşınsın.
2. **`SegmentSubjectSnapshot`'a additive `Workplace`** (primary account adı) ekle; resolver/candidate-source snapshot'u kurarken contact için primary link'ten `AccountName` doldursun (account subject'te null). **Ekstra Mongo read YOK** (mevcut link+account bulk read'ten).
3. **`SecondaryLabel` (SEG-D) revize:** contact → `Specialty` (yoksa ProfessionalTitle) **· Workplace** (varsa; yoksa ·City fallback); account → `Type` (yoksa Category) · City (aynı). Salt görüntü.
4. Link projeksiyonu **join attribute olmasa da** sample için gerekli: eğer LoadLinksAsync yalnız join attribute varken çağrılıyorsa, **sample workplace için** de contact candidate'larında link bulk oku (tek bulk, sample değil tüm candidate contact'lar zaten link okunuyorsa reuse; okunmuyorsa contact resolution'da 1 bulk link+account-name read — per-candidate DEĞİL). Performans: bulk, count-only path etkilenmez.

## KORU / YAPMA
- `/resolve` member SET/ORDER/count/reason DEĞİŞMEZ; `SubjectSecondaryLabel` additive nullable (SEG-D) — yalnız içerik zenginleşir. Resolution logic/pushdown/cap/PII gate DEĞİŞMEZ. **Per-candidate (N+1) read EKLEME** — yalnız bulk. Kural değerlendirmesine workplace GİRMEZ (salt görüntü). Payload/catalog/reach + proxy + frontend DEĞİŞMEZ (frontend zaten `subjectSecondaryLabel` gösteriyor — SEG-D). Başka modül.

## Acceptance
- **E2:** CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail. Contact sample/member `SubjectSecondaryLabel` = "Specialty · Workplace(account adı)"; account = "Type · City". Ekstra read yok (mevcut link+account bulk'a Name alanı); /resolve additive; per-candidate read yok.
- **E4:** ALMIBA nefrolog sample → "Nephrology · <hastane adı>".

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-F · Sample ikincil etikete workplace (bağlı account adı) (MOD-0167-FU02, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (d63befa5 üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-F-sample-workplace.md · Persistence/Repositories/SegmentCandidateSource.cs (LoadLinksAsync ~141-157 linked-account bulk read, Project Include AccountType) · Resolution/SegmentLinkProjection.cs + SegmentSubjectSnapshot.cs (SecondaryLabel SEG-D) + SegmentMembershipResolver.cs.

NE (backend, ekstra read YOK — mevcut bulk'a alan):
 1) SegmentLinkProjection'a additive AccountName; SegmentCandidateSource linked-account bulk read Project(Include("AccountType","Name")); primary link account adı taşı.
 2) SegmentSubjectSnapshot'a additive Workplace (contact primary account adı; account subject null); snapshot kurarken primary link'ten doldur.
 3) SecondaryLabel (SEG-D) revize: contact→Specialty(yoksa ProfessionalTitle)·Workplace(varsa; yoksa ·City); account→Type(yoksa Category)·City. Salt görüntü.
 4) Link bulk sample için gerekli: join attribute olmasa da contact candidate'larda link+account-name BULK oku (per-candidate DEĞİL; reuse varsa reuse). count-only path etkilenmez.
KORU/YAPMA: /resolve member SET/ORDER/count/reason değişmez; SubjectSecondaryLabel additive nullable (SEG-D), yalnız içerik zenginleşir; resolution/pushdown/cap/PII gate değişmez; N+1/per-candidate read EKLEME (yalnız bulk); workplace kural değerlendirmesine GİRMEZ; payload/catalog/reach/proxy/frontend değişmez; başka modül.
DOĞRULA (E2): TAM CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail; contact secondaryLabel="Specialty · Workplace", account="Type · City"; ekstra read yok (mevcut bulk+Name); /resolve additive; per-candidate read yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: account adı mevcut bulk'a eklenemiyorsa; workplace per-candidate read gerektiriyorsa (DUR+raporla, alternatif sun); /resolve şekli bozuluyorsa; kapsam Segmentation dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-16) → **ACCEPTED (E2)**
```text
Commit: 996b277e · Agent: PASS (--no-build) · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-segef-verify @996b277e (SEG-E+F birlikte)
```
- ✅ **Scope:** 6 backend dosyası (LinkProjection/CandidateSource/Snapshot/AttributeSourceReader/Resolver + interface). Frontend'e dokunulmadı (SEG-D `subjectSecondaryLabel`'ı zaten gösteriyor).
- ✅ **Ekstra read YOK:** `AccountName` mevcut linked-account bulk read'e `Project(Include("AccountType","Name"))` — tek projeksiyon alanı, per-candidate yok. `LinkProjection.AccountName` additive nullable.
- ✅ **N+1 yok:** resolver linkleri **bir kez** bulk okur (yalnız üye döndüren contact path, `limit>0`); reader'a `preloadedLinks` paslar (join'li segmentte çift-okuma yok); count-only funnel (`limit==0`) etkilenmez. Sözleşme testi `Every_derived_source_is_read_exactly_once` (LoadLinksCalls==1) geçti.
- ✅ **Snapshot additive:** `Workplace { get; init; }` (pozisyonel ctor değişmedi → test builder'lar sağlam). SecondaryLabel contact → `Specialty(·Workplace; yoksa ·City)` — Workplace null'da eski SEG-D davranışı korunur; account → Type·City değişmedi. Salt görüntü (kural değerlendirmesine girmez).
- ✅ **/resolve additive:** member SET/ORDER/count/reason değişmedi (yalnız SubjectSecondaryLabel içeriği zenginleşti).
- ✅ **Build+test (CT izole, Release, GERÇEK build):** CrmService.Application.Tests **1740/0/5** + Diten.Web.Tests **137/0**.
- ⏳ **E4:** ALMIBA nefrolog sample → "Nephrology · <hastane adı>".
