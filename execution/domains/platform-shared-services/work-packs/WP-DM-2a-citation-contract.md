# WORK PACKAGE — WP-DM-2a · Controlled Document Citation contract (SABİT + teslim)

> **Control Tower kaydı (SoR).** DM planı **kritik yolu** — TaskCenter repoint'i buna bağlı başlar. Module: **MOD-0029** (Document Master Register). Owner: doc-mgmt lead. Branch: `feature/structured-content-messaging` (KARAR-1/W-DM-BRANCH).
> **İzolasyon:** `DocumentManagementMasterRegister` feature'ı MOD-0262 dirty işiyle ÇAKIŞMIYOR (ölçüldü) → bu branch'te güvenli. MOD-0262 dirty dosyalarına DOKUNMA.

## Metadata
```text
WP ID:            WP-DM-2a
Prompt ID:        P-DM-2a · v1.0
Task Class:       Backend contract (frozen, to be delivered) — Profile B
Risk Class:       MEDIUM (imza bir kez sabitlenince değişmemeli — tüketici buna yazacak)
Agent Lane:       AL-DM-CITATION (DEV) · Target Agent: backend-architect
Branch:           feature/structured-content-messaging · Expected HEAD: (dispatch anındaki HEAD)
Persistence:      no writes (read-only resolver + port)
```

## Ölçülmüş girdi
- **Desen (mirror):** `DocumentManagementMasterRegister/Services/IControlledDocumentEffectivenessPort.cs` + `ResolveDocumentEffectivenessQuery` + `Models/DocumentEffectivenessModels.cs` — disjoint + fail-closed, **bu branch'te MEVCUT**. Citation bunun **zengin kardeşi** (sadece state değil, tam item döner).
- `DocumentMasterRegisterEntry`: PermanentUid, DocumentCode, (Title/Version), LifecycleStatus, ControlledDocumentId. `GetByPermanentUidsAsync` (batch $in) **var** → Resolve reuse.
- `Citable = ControlledDocumentLifecyclePolicy.IsOperationallyEffective` (Effective ∨ UnderRevision) — effectiveness gate ile **aynı yargı kaynağı**; status-mapping'den bağımsız.
- Benzetilecek: Tasks `DocumentReferenceEntryDto` (mevcut governing-docs/picker DTO'su).

## Kapsam (SABİT SÖZLEŞME + teslim)
1. **`IControlledDocumentCitationPort`** (`Features/DocumentManagementMasterRegister/Services/`):
   - `ResolveAsync(IReadOnlyCollection<string> identifiers, DocumentIdentifierKind by, ct)` → governing-docs + freezer.
   - `SearchAsync(string term, int limit, ct)` → picker.
2. **`DocumentCitationItem`** = `{ Uid, Code, Title, Version, Lifecycle, Citable, BlockedReason, Source (=MasterRegister sabiti), RegisterEntryId? }` — bilerek `DocumentReferenceEntryDto`'ya benzer.
   - `Citable = IsOperationallyEffective(LifecycleStatus)`; `BlockedReason` = citable değilse LifecycleStatus adı.
   - `Source = MasterRegister` (freezer'lar bunu set eder; `ListVersionId=null`).
3. Query+handler: `ResolveDocumentCitationQuery` (batch `$in` — `GetByPermanentUidsAsync`/`GetByDocumentCodesAsync` reuse) + `SearchDocumentCitationQuery` (register repo term filtresi).
4. **SearchAsync davranışı:** blocked doküman **görünür ama seçilemez** (lookup davranışını aynala — picker'da gösterilir, citable=false).
5. **Fail-closed (effectiveness deseni):** repo istisnası **fırlatılır** (Unresolved'a çevrilmez); tohum boşken **Unresolved/boş** döner ama **şekil sabit**. Sessiz varsayılan yok (`by` açık).

## SÖZLEŞME STABİLİTESİ (kritik)
İlk teslim = register-backed minimal impl + testler + **imza dokümante**. Bir kez sabitlenince **imza DEĞİŞMEZ** (tüketici/TaskCenter buna yazar, K16). Tohum boş olsa da şekil sabit → onlar fixture/mock ile repoint'e başlar. **Hazır olunca CT'ye haber → CT doğrular → "teslim edildi" işaretlenir.**

## Acceptance
- E2: build temiz; unit — Resolve by=uid/code doğru item; Citable=IsOperationallyEffective (Effective/UnderRevision→true, diğer 7→false+reason); Unresolved (kayıt yok); Search term filtreler + blocked görünür/citable=false; repo-throws→propagate (fail-closed); Source=MasterRegister; port==query.
- Regresyon: tam Platform.Application.Tests — yeni fail yok.
- **Teslim:** imza + DTO dokümante (bu WP + kod XML doc); tohum boşken şekil-sabit davranış kanıtlı.
- E4 (fleet): opsiyonel — port-only (HTTP uç bu WP'de yok; governing-docs/picker HTTP = follow veya TaskCenter tarafı).

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-DM-2a · Prompt P-DM-2a v1.0  (Controlled Document Citation contract — MOD-0029, FROZEN + deliver)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: <dispatch anındaki HEAD> · Worktree: ana checkout
⚠ Bu tree'de MOD-0262 (ControlledDocuments/Registration/Template/Versioning + silinmiş IContentStorageGateway) COMMIT'SİZ
  dirty iş var — o dosyalara DOKUNMA. Sen yalnız DocumentManagementMasterRegister feature'ında çalış (izole).

Önce oku (mirror deseni):
1. services/Diten.Platform/.../DocumentManagementMasterRegister/Services/IControlledDocumentEffectivenessPort.cs
   + ControlledDocumentEffectivenessPort.cs + Queries/ResolveDocumentEffectivenessQuery.cs + Models/DocumentEffectivenessModels.cs
2. services/Diten.Platform/.../Domain/Entities/DocumentManagement/DocumentMasterRegisterEntry.cs + Enums/.../ControlledDocumentLifecyclePolicy.cs
3. services/Diten.Platform/.../Domain/Repositories/IDocumentManagementMasterRegisterRepositories.cs (GetByPermanentUidsAsync/GetByDocumentCodesAsync)
4. services/Diten.Platform/.../Features/Tasks/.../DocumentReferenceListQueryHandlers.cs (DocumentReferenceEntryDto — benzetilecek şekil)

NE: Effectiveness resolver'ın ZENGİN KARDEŞİ olarak citation contract kur (SABİT, teslim edilecek):
 1) IControlledDocumentCitationPort: ResolveAsync(identifiers, by, ct) [governing-docs+freezer] · SearchAsync(term, limit, ct) [picker].
 2) DocumentCitationItem { Uid, Code, Title, Version, Lifecycle, Citable, BlockedReason, Source(=MasterRegister), RegisterEntryId? } —
    DocumentReferenceEntryDto'ya benzer. Citable = IsOperationallyEffective(LifecycleStatus); BlockedReason = citable değilse status adı.
 3) ResolveDocumentCitationQuery (batch $in — GetByPermanentUidsAsync/GetByDocumentCodesAsync reuse) + SearchDocumentCitationQuery (term filtre).
 4) SearchAsync: blocked doküman GÖRÜNÜR ama citable=false (picker davranışı).
NEDEN: TaskCenter repoint'inin bağlı olduğu KRİTİK YOL sözleşmesi; freezer/governing-docs/picker hepsi bunu tüketir.
NASIL: effectiveness resolver'ı birebir örnek al (disjoint, fail-closed, Response<T>, port=query üstünde ince adapter).
       Citable status-mapping'den BAĞIMSIZ (effectiveness gate ile aynı IsOperationallyEffective). no writes.
YAPMA: MOD-0262 dirty dosyalarına dokunma; imzayı "geçici" tutup sonra değiştirme (SABİT — teslim); MODd-0031 evidence entegrasyonu
       (ref-only, ayrı); silent default (by açık); register'a yazma.
DOĞRULA (E2):
 - build temiz; unit: Resolve by=uid/code doğru item · Citable (Effective/UnderRevision→true, diğer 7→false+reason) · Unresolved(yok) ·
   Search term filtreler + blocked görünür/citable=false · repo-throws→propagate (fail-closed) · Source=MasterRegister · port==query.
 - tam Platform.Application.Tests: yeni fail YOK.
 - İMZA DOKÜMANTE (XML doc + kısa özet) → teslim; tohum boşken şekil-sabit davranış kanıtlı.
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13); imza CT onayından sonra "teslim edildi".

Durma koşulları: register entry'de Title/Version alanı yoksa (raporla, uydurma) · effectiveness deseni bulunamazsa · kapsam MasterRegister dışına taşarsa (MOD-0262 dirty!). DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-10) → **ACCEPTED (E2) — SÖZLEŞME TESLİM EDİLDİ**
```text
Commit: 2317464b · Agent: PASS · Verification: PASS · CT: ACCEPTED · Evidence: E2
```
- ✅ Scope: 7 dosya, hepsi DocumentManagementMasterRegister + DI + test — **MOD-0262 dirty (12 dosya) commit'e GİRMEDİ** (izole).
- ✅ **İmza WP'ye uygun + tam:** IControlledDocumentCitationPort (ResolveAsync+SearchAsync) · DocumentCitationItem{Uid,Code,Title,Version,Lifecycle,Citable,BlockedReason,Source=MasterRegister,RegisterEntryId?} · Citable=IsOperationallyEffective · fail-closed.
- ✅ **CT kendi koşumu (izole worktree):** DocumentCitation **20/20**; **tam suite full-run'da DocumentCitation fail = 0** → ajanın "154→159 +5" farkı **gerçek env-flake** (156 taban = önceden var macOS-path/mongo, DM-2a değil — DocumentCitation temiz).
- HEAD drift benign (base 9bd9d17b ata; araya SCMM-12/3ad3ede7 girdi, MOD-0162, kapsamsız).
- ⏳ **A.2 SÖZLEŞME TESLİM EDİLDİ** → handoff'a "A.2 delivered" + TaskCenter'a haber verilebilir (imza SABİT, K16 — değişmez).

## Kalan (bu WP dışı)
- DM-1 (ingest + auto-seed, paralel) · DM-2b (Search/Resolve gerçek impl, tohum sonrası canlı) · DM-3 (effectiveness doğrula) · DM-4 (gerçek dosya, MOD-0262 ile).
- Governing-docs/picker HTTP uç (TaskCenter tarafı bu portu tüketir).
