# WORK PACKAGE — WP-CL-BE-5 · İddia ↔ kanıt (MOD-0031): bağla/kaldır proxy · miras · "en az 1 kanıt" · belge değişince "gözden geçirilmeli" (okumada) · süresi dolan kanıt (CRM + küçük Platform)

> **CT (SoR).** İddialar v2 Faz 2 son paket. Kaynaklar:
> - `SCMM-claims-v2-mockup-analysis-plan.md` (CL-BE-5, senaryolar 5 / 6 / 10)
> - `SCMM-claims-approval-evidence-roadmap.md` §B2/§B4
> - Üzerine kurulduğu paketler: **BE-1** (model), **BE-2** (MOD-0031), **BE-4** (onay)
>
> **Amaç:**
> - İddia (çekirdek, yerel) ve ülke sürümü, MOD-0031 üzerinden **belgenin sabit sürümüne** kanıt bağlasın.
> - Ülke sürümü çekirdeğin kanıtlarını **miras** alsın.
> - Onaya gönderim **en az 1 kanıt** istesin.
> - Onaylı / incelemedeki kayıtta kanıt **kilitlensin**.
> - Kanıt belgesinin **yeni sürümü** çıktığında ya da belge **askıya alındığında / geri çekildiğinde**, onaylı kayıt **"gözden geçirilmeli"** olsun.
> - Süresi dolmak üzere olan kanıt işaretlensin.
>
> **Belge değişimini nasıl anlayacağız:** Belge Yönetimi **değişiklik olayı yayınlamıyor** (CT teyit: DocMgmt'te sürüm-yürürlük ya da askı integration event'i yok). Bu yüzden karşılaştırma **okumada** yapılır: MOD-0031 kanıt okumasında sabitlenen sürüm ile belgenin güncel durumu karşılaştırılır.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-5`, dal `wp/cl-be-5`. Commit bu dala, push YOK.

## Kanıt (CT kod okuması)
- **MOD-0031 (BE-2):** `Platform.Application/Features/EvidenceLinking/**`.
  - `EvidenceLink{ObjectRef{Module, ObjectType, ObjectId, ObjectVersion}, DocumentKind, DocumentId, DocumentVersionId, DocumentVersionLabel, EvidenceTypeCode, Locator, SupportedSpans, Status}`.
  - Uçlar: `api/v1/evidence/links` (POST, GET?module&objectType&objectId), `links/{id}/remove`, `links/by-document/{id}`, `document-options`.
  - Erişim kontrolü `EvidenceDocumentAccessGate` (DocMgmt `DocumentAccessEvaluator.CanReadControlledDocumentAsync`).
- **DocMgmt:**
  - `ControlledDocumentVersion{DocumentId, VersionNumber, VersionStatus, DeletedAt}`.
  - Master register yaşam döngüsü (Effective / Suspended / Retired…): `Features/DocumentManagementLifecycle`, `DocumentManagementSuspension`.
  - Periyodik gözden geçirme: `DocumentManagementPeriodicReview`.
  - Harici belge `ExternalDocumentRegisterEntry{SourceVersion, SourceStatus, SourceSupersededDate, izleme tarihleri}`.
  - Web tarafında "güncel sürüm" çözümü var (`KnowledgeController.ResolveCurrentVersionIdAsync`, `document-preview`). Platform tarafında eşdeğer okuma bulunacak.
- **CRM:**
  - BE-4 Platform HTTP istemcisi (kullanıcı token'ı + X-Tenant-Id iletimi) → aynı desenle **evidence istemcisi**.
  - `Claim` / `ClaimCountryVersion` durumları: approved / review-required / in-review / draft…
  - Onaya gönderim komutları: BE-4 `submit-review`.
  - Okumada uzlaştırma deseni: BE-4 reconcile-on-read.
  - Hata biçimi: `[code, message]`.

## Kararlar
- **ObjectRef** (CRM belirler, istemci belirleyemez):
  - çekirdek / yerel: `{Module:"crm", ObjectType:"claim", ObjectId:claimId, ObjectVersion:ClaimVersion}`;
  - ülke sürümü: `{Module:"crm", ObjectType:"claim-country-version", ObjectId:versionId, ObjectVersion:Version}`.
- **Miras:** ülke sürümünün etkin kanıtları = **bağlı çekirdek kaydın** aktif kanıtları (salt okunur, `origin:"core"`) + kendi kanıtları (`origin:"local"`). Yerel iddiada miras yok.
- **Kilit:** kayıt `in-review`, `approved`, `review-required`, `inactive` ya da `archived` ise kanıt ekleme/kaldırma → **409 `evidence_locked`** (değişiklik = yeni sürüm). Yalnız `draft`'ta değişir. **Yeni sürüm açılınca önceki sürümün kendi kanıtları yeni sürüme kopyalanır** (MOD-0031'e yeni bağ olarak; sabit sürümler korunur).
- **Onaya gönderim kuralı** (BE-4 `submit-review`'e eklenir): etkin kanıt sayısı (çekirdek/yerel: kendi; ülke sürümü: miras + kendi) **≥ 1**, aksi **409 `evidence_required`**.
- **Belge değişimi — okumada** (Platform tarafında küçük ekleme):
  - MOD-0031 link okuma DTO'suna **hesaplanan** alanlar eklenir:
    - `currentVersionId`, `currentVersionLabel`;
    - `isSuperseded` (sabitlenen sürümden **daha yeni yürürlükte sürüm** var);
    - `documentState` (`effective` / `suspended` / `retired` / `withdrawn` / `unknown`);
    - `reviewDueAt?` (kontrollü: periyodik gözden geçirme tarihi; harici: izleme ya da SourceSupersededDate).
  - Kaynak **DocMgmt'in mevcut okumaları**; yeni DocMgmt yazımı yok.
  - CRM okuma sırasında (liste, detay, coverage; BE-4 uzlaştırmasıyla aynı noktada), **onaylı** bir kaydın etkin kanıtlarından biri `isSuperseded` ya da `documentState ∈ {suspended, retired, withdrawn}` ise kayıt **review-required** yapılır:
    - `ApprovedAt` korunur;
    - audit `claim_review_required_evidence_changed` (linkId, documentId; metin yok);
    - idempotent;
    - çekirdekte BE-1 yayılımı **tetiklenmez** (yalnız kaydın kendisi).
- **Süresi dolan kanıt:** `reviewDueAt ≤ bugün + Crm:Claims:ExpiringWindowDays (60)` ise okumada `evidenceExpiring=true`. Durum **değişmez**, yalnız işaret.
- **Performans:** okuma başına Platform'a **toplu** evidence çağrısı yapılır (sayfa başına tek istek). Yoksa Platform'a toplu ObjectRef sorgusu eklenir (aşağıda). Platform yoksa okuma bozulmaz.

## NE

### A. Platform (küçük, MOD-0031 okuma ekleri)
1. `EvidenceLinkDto`'ya hesaplanan alanlar: `currentVersionId`, `currentVersionLabel`, `isSuperseded`, `documentState`, `reviewDueAt`. Bunlar DocMgmt'in mevcut sürüm, yaşam döngüsü, askı ve periyodik gözden geçirme okumalarından hesaplanır. Harici belgede `SourceStatus` / `SourceSupersededDate` / izleme tarihi kullanılır.
2. **Toplu okuma:** `POST api/v1/evidence/links/query` `{ objects:[{module, objectType, objectId}] (≤100), includeRemoved:false }` → nesne başına bağ listesi (hesaplanan alanlarla). Mevcut GET aynen kalır.
3. Testler + BE-2 §37'deki 26 testin bozulmaması.

### B. CRM
4. **Evidence istemcisi** (BE-4 HTTP deseni; token + tenant iletimi): link, remove, query (toplu).
5. **Uçlar** (`crm.claim.manage` / `crm.claim.read`):
   - `GET claims/{id}/evidence` · `GET claims/country-versions/{id}/evidence` → `{ items:[{…EvidenceLinkDto, origin: core|local}], effectiveCount, anyNeedsReview, anyExpiring }`.
   - `POST claims/{id}/evidence` · `POST claims/country-versions/{id}/evidence` → gövde `{documentKind, documentId, documentVersionId?, evidenceTypeCode, locator, supportedSpans}`. **ObjectRef'i CRM doldurur.** Kilit kontrolü `evidence_locked`.
   - `POST claims/evidence/{linkId}/remove {reason}` → bağın bu kayda ait olduğu doğrulanır (başkasının bağı → 404). Kilit kontrolü.
   - `GET claims/evidence/document-options?search&kind` → MOD-0031 `document-options` geçişi.
6. **submit-review'e kanıt kuralı** (`evidence_required`). BE-4 hata sırası korunur. Kanıt kontrolü BE-4 kurallarından sonra, Platform çağrısından önce yapılır.
7. **Yeni sürümde kopyalama:**
   - Çekirdek `new-version` ve ülke `new-version` handler'larında önceki kaydın **kendi** aktif kanıtları yeni ObjectRef ile yeniden bağlanır (MOD-0031 POST).
   - Kopya başarısızsa yeni sürüm **yine oluşur**; eksik kanıt raporda ve audit'te görünür.
   - **Kopya başarısızlığı onaya gönderimde `evidence_required` ile yakalanır.**
8. **Okumada kanıt değerlendirmesi:** liste, detay, coverage okumalarında onaylı kayıtlar için toplu query → `isSuperseded` / `documentState` → review-required. `evidenceExpiring` bayrağı liste, detay ve coverage DTO'larına eklenir. Platform yoksa okuma bozulmaz, log düşülür.
9. **Audit:** `claim_evidence_linked / removed / copied / review_required_evidence_changed` (id, sayı; alıntı ve metin yok).

## KORU / YAPMA
- **DocMgmt yazımı YOK.** DocMgmt'e olay ekleme YOK (ayrı iş). MOD-0031'in mevcut uçları, kuralları ve erişim kontrolü DEĞİŞMEZ (yalnız DTO ekleri ve toplu okuma).
- BE-1 / BE-4 / BE-6 kuralları ve hata kodları korunur. Onay mantığı yalnız `ApplyReviewOutcome`'da.
- **ObjectRef istemciden alınmaz.** Başka kaydın bağı kaldırılamaz.
- Web, Auth, gateway DOKUNMA. Mevcut `/api/v1/evidence/{everything}` route'u toplu okumayı kapsar.
- **DUR:**
  - DocMgmt'te "güncel yürürlükteki sürüm" ya da askı/geri çekme durumu için **tek güvenilir okuma** yoksa → `documentState=unknown`, `isSuperseded=false` dön ve raporla. Tahmin etme.
  - Yeni sürümde kopyalama MOD-0031'in "tekrar" (409) kuralına takılıyorsa (aynı sürüm + alıntı + farklı ObjectRef) → dur ve raporla.

## Acceptance
- **E2:**
  - CRM: `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo` → **0 kırmızı** (taban 2040/0/5 + yeni).
  - Platform: `…/Diten.Platform.Application.Tests` → **yeni kalıcı kırmızı yok** (taban 173 ortam kırmızısı; TRX karşılaştırması; Mongo dalgalanması tekrar koşuyla ayrılır).
  - Web 229/0.
  - CRM ve Platform API build 0 hata.
- **Yeni testler:**
  - **Platform:** yeni sürüm yürürlüğe girince `isSuperseded`; askı → `suspended`; harici `SourceSupersededDate` → superseded; toplu okuma ≤100 + kiracı izolasyonu.
  - **CRM:**
    - bağla (ObjectRef CRM'den), kilit → `evidence_locked`, başkasının bağını kaldırma → 404;
    - miras (ülke sürümü çekirdek kanıtlarını görür, `origin`);
    - submit kanıtsız → `evidence_required`, miras kanıtı sayılır;
    - yeni sürümde kopyalama;
    - okumada superseded / suspended → review-required (idempotent, `ApprovedAt` korunur, çekirdek yayılımı yok);
    - expiring bayrağı;
    - Platform erişilemezken okuma bozulmuyor.
  - **Sabotaj kanıtı** en az 3 kural için.
- **Diff:** `services/Diten.CrmService/**` + `services/Diten.Platform/**` (yalnız EvidenceLinking okuma ekleri).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-5 · İddia ↔ kanıt (MOD-0031): bağla/kaldır proxy · miras · "en az 1 kanıt" · belge değişince "gözden geçirilmeli" (okumada) · süresi dolan kanıt (CRM + küçük Platform)
Repository: C:\tmp\cl-be-5 (worktree) · Branch: wp/cl-be-5 · commit bu dala, push YOK

Amaç: İddia/ülke sürümü MOD-0031 ile belgenin SABİT sürümüne kanıt bağlasın; ülke sürümü çekirdek kanıtlarını miras alsın; onaya gönderim ≥1 kanıt istesin; onaylı/incelemedeki kayıtta kanıt kilitli; belge yeni sürümü/askı/geri çekme OKUMADA karşılaştırılıp onaylı kayıt review-required olsun (DocMgmt olay yayınlamıyor); süresi dolan kanıt işaretlensin.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-5-claims-evidence.md (Kararlar + NE A/B + hata kodları) · …/WP-CL-BE-2-mod0031-evidence-linking.md · …/WP-CL-BE-4-claims-workflow-approval.md · services/Diten.Platform/src/Diten.Platform.Application/Features/EvidenceLinking/** · Features/DocumentManagement{Lifecycle,Suspension,PeriodicReview,ControlledDocuments,ExternalDocuments}/** (güncel sürüm + durum okumaları) · Domain/Entities/DocumentManagement/{ControlledDocumentVersion,ExternalDocumentRegisterEntry}.cs · services/Diten.CrmService/src/** (BE-4 Platform HTTP istemcisi + reconcile-on-read, submit-review handler'ları, new-version handler'ları, Claim/ClaimCountryVersion).

NE:
 A1) Platform EvidenceLinkDto'ya hesaplanan alanlar: currentVersionId, currentVersionLabel, isSuperseded, documentState (effective|suspended|retired|withdrawn|unknown), reviewDueAt — DocMgmt'in MEVCUT okumalarından (harici: SourceStatus/SourceSupersededDate/izleme).
 A2) POST api/v1/evidence/links/query {objects[≤100], includeRemoved} → nesne başına bağlar (hesaplanan alanlarla); mevcut GET aynı.
 B4) CRM evidence istemcisi (BE-4 deseni, token+tenant): link/remove/query.
 B5) Uçlar: GET claims/{id}/evidence + claims/country-versions/{id}/evidence → items[+origin core|local], effectiveCount, anyNeedsReview, anyExpiring · POST claims/{id}/evidence + claims/country-versions/{id}/evidence (ObjectRef CRM doldurur; draft dışı 409 evidence_locked) · POST claims/evidence/{linkId}/remove {reason} (başkasının bağı 404; kilit) · GET claims/evidence/document-options?search&kind.
 B6) submit-review'e ≥1 etkin kanıt kuralı (çekirdek/yerel: kendi; ülke sürümü: miras+kendi) → 409 evidence_required (BE-4 kurallarından sonra, Platform çağrısından önce).
 B7) new-version (çekirdek + ülke) handler'larında önceki kaydın KENDİ aktif kanıtlarını yeni ObjectRef'le yeniden bağla; kopya başarısızsa sürüm yine oluşur (audit + submit'te evidence_required yakalar).
 B8) Okumada (liste/detay/coverage, BE-4 uzlaştırmasıyla aynı noktada): onaylı kayıtların etkin kanıtları toplu query → isSuperseded veya documentState suspended/retired/withdrawn → review-required (ApprovedAt korunur, idempotent, çekirdek yayılımı YOK, audit claim_review_required_evidence_changed); evidenceExpiring (reviewDueAt ≤ bugün+ExpiringWindowDays) DTO'lara; Platform yoksa okuma bozulmaz.
 B9) Audit claim_evidence_linked/removed/copied/review_required_evidence_changed (metinsiz).
KORU/YAPMA: DocMgmt yazımı/olay ekleme YOK; MOD-0031 mevcut uç/kural/erişim DEĞİŞMEZ (yalnız DTO eki + toplu okuma); BE-1/4/6 kural ve kodları korunur; ObjectRef istemciden alınmaz; Web/Auth/gateway DOKUNMA.
DOĞRULA (E2): CRM tests → 0 kırmızı (taban 2040/0/5 + yeni); Platform Application tests → yeni kalıcı kırmızı yok (taban 173 ortam kırmızısı, TRX karşılaştır, Mongo dalgalanmasını tekrar koşuyla ayır); Web 229/0; CRM+Platform API build 0 hata; yeni testler WP Acceptance listesindeki gibi + ≥3 sabotaj; git diff yalnız services/Diten.CrmService/** + services/Diten.Platform/** (EvidenceLinking okuma ekleri). Commit ("feat(crm): WP-CL-BE-5 — claim evidence via MOD-0031 (link/inherit/lock, ≥1 evidence to submit, read-time supersede/suspend → review-required, expiring flag)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: DocMgmt'te güncel yürürlükteki sürüm/askı için tek güvenilir okuma yoksa documentState=unknown + isSuperseded=false + raporla (tahmin etme); yeni sürüm kopyası MOD-0031 409 tekrar kuralına takılırsa DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (E2)**
```
Commit: 0baafa75 · Agent: PASS (CRM 2085/0/5, Platform 5035/174 taban-eşit + Mongo dalgalanması, Evidence 42/0, Web 229/0, 6 sabotaj)
CT: ana dal birleştirildi (çakışmasız) → CRM+Platform API build 0 hata · CRM 2085/0/5 (ilk koşuda 1 dalgalanma, 2 tekrar koşuda 0) · Platform Evidence+Workflow 217/0 · Web 229/0
```
- ✅ **Kapsam:** 26 dosya. `services/Diten.CrmService/**` + Platform'da yalnız kanıt dosyaları (EvidenceLinksController, EvidenceLink repo, DI, EvidenceLinking/**). DocMgmt yazımı YOK.
- ✅ **Durum çözücü** (`EvidenceDocumentStateResolver`, CT okudu):
  - Master register `LifecycleStatus` + `ControlledDocument.CurrentVersionId` + sabit sürümün `VersionStatus=Superseded` kullanılıyor.
  - UnderRevision → eski sürüm geçerli.
  - Superseded / Retired / ObsoleteCopy → retired.
  - Register satırı yoksa → `unknown` + superseded değil (tahmin yok).
  - Harici belge: SourceStatus / ExternalDocumentStatus / SourceSupersededDate.
- ✅ **Kurallar:**
  - ObjectRef'i CRM dolduruyor.
  - `evidence_locked`; başka kaydın bağı → 404.
  - `evidence_required`; kanıt servisi kapalıysa **503 `evidence_unavailable`** (kural sessizce geçilmiyor).
  - Yeni sürümde kopya.
  - Okumada review-required (idempotent, ApprovedAt korunur).
  - `EvidenceExpiring`.
- ➕ **Agent kararları (kabul):**
  - Çekirdek kanıtı değişince onu miras alan onaylı ülke sürümü de review-required olur. Sebep ülke sürümünün kendi etkin kümesinin değişmesi; BE-1 yayılımı çalışmıyor.
  - Review-required çekirdekten yeni sürüm açılabiliyor.
  - Yeni onayda eski review-required kayıt pasife alınıyor.
- ✅ **Yetki:** `platform.evidence.links.*` 97c5 Admin rolüne CFG-1 ile verildi. Kullanıcıların yeniden girişi gerekli.
