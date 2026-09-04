# WORK PACKAGE — WP-0029-EFFECTIVENESS-F1

> Control Tower kaydı (SoR). Bu chat kayıt yeri değildir (K11). Authority, scratchpad'den bu
> repoya taşınmıştır (DEC-B, 2026-09-04): bkz. `authority/` klasörü.

## Metadata (§17.1 / §36)

```text
WP ID:            WP-0029-EFFECTIVENESS-F1
Prompt ID:        P-EFF
Prompt Version:   v1.0
Task Class:       Backend contract (read-only resolver)
Golden-Flow Profile: B
Risk Class:       MEDIUM

Capability:       DCP-005 — Task ↔ Controlled Document Reference (doküman-yönetimi tarafı, Adım 1 / Faz 1)
Module:           MOD-0029 (Document Master Register, FU06 vokabülerini tüketir) · tüketici MOD-0024
Build Sequence:   Faz 1 (Faz 2–5 sonra)
Build Lane:       BL-0029-EFFECTIVENESS
Agent Lane ID:    AL-0029-EFF-F1
Agent Lane Type:  DEV
Target Agent / Entry Point: backend-architect  (@[.antigravity/agents/backend-architect.md])

Target Branch:      feature/crm-integration-v2      # DEC-A (owner override; §16.2 waiver W-EFF-BRANCH)
Expected Base HEAD: 579ba4468046559d6e7f20fba0b3cc369a63c0cc
Worktree:           C:\Users\user\Desktop\ERP-vNext (main checkout)
Dirty baseline:     165 dosya kirli (105 M + 60 untracked), ağırlıkla CRM. Faz 1 allowed-path'leriyle
                    ÇAKIŞMA YOK (tek dirty Platform src = Features/Crm/SelfRegistration/CrmManifestProvider.cs;
                    DependencyInjection.cs TEMİZ). Ölçüldü 2026-09-04.

Depends On:         yok (Faz 1 join kararını/G1'i beklemez; a/b-agnostik, fixture/mock ile)
Parallel-Safe With: CRM dirty tree (farklı dosyalar). NOT parallel-safe with: Faz 2 (aynı feature klasörü, single-writer §16.4)
Integration Order:  Faz 1 → Faz 2 → Faz 3 → Faz 4 → Faz 5; Görev Merkezi Adım 2–3 en sonda

Authority Sources:
- Identity:      module-id-registry — MOD-0029 (Document Management)
- DCP:           execution/portfolio/delivery-capability-packs/DCP-005-task-document-reference.md (status: draft)
- Contract:      execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-contract-v2.md
- Work plan:     execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-work-plan-adim0-adim1.md
- Provenance:    dört QA/ERP yazışması + is-plani/sonuc, orijinal scratchpad session 90bcdfc8 (2026-09-03/04)

Allowed Paths:
- services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/**  (yeni dosyalar)
- services/Diten.Platform/src/Diten.Platform.Application/DependencyInjection.cs                        (yalnız port kaydı)
- services/Diten.Platform/tests/Diten.Platform.Application.Tests/**                                    (yeni testler)

Protected Paths / YAPMA:
- IDocumentManagementMasterRegisterRepositories.cs (interface) — DOKUNMA (batch = Faz 2)
- HTTP controller / ocelot / route — DOKUNMA (Faz 3)
- Permission sabiti / DataSeeder / katalog — DOKUNMA (Faz 4)
- Register verisi / DocumentMasterRegisterEntry yazımı — DOKUNMA (Faz 5 / Adım 0)
- CSV / DocumentReferenceEntry / Tasks tarafı / mevcut 2 donmuş satır — DOKUNMA
- 165 dirty CRM dosyası — DOKUNMA
```

## Ölçülmüş kod sözleşmesi (K2 — file:line, 2026-09-04)

| Fakt | Kanıt |
|---|---|
| Repo tekil var, batch yok | `IDocumentManagementMasterRegisterRepositories.cs` L23/L26; `GetAllForTenantAsync` L32 |
| IsOperationallyEffective = Effective ∨ UnderRevision | `ControlledDocumentLifecyclePolicy.cs:16` |
| ControlledDocumentLifecycleStatus = 9 üye (2 effective → 7 Blocked) | `MasterRegisterEnums.cs:40-51` |
| Tasks + DocMgmt aynı assembly | `Diten.Platform.Application/Features/{Tasks,DocumentManagementMasterRegister}` |
| Gerçek klasör | `Features/DocumentManagementMasterRegister/` (planın `DocumentManagement/MasterRegister` yolu YANLIŞ) |
| effectiveness.read izni yok | `DocumentMasterRegisterPermissions` = View/Manage/Link/AuditView (Faz 4'te eklenecek) |

## Donmuş kararlar

- **D-FAZ1-REPO (DEC-C, onaylı):** Faz 1 çözümlemeyi mevcut `GetAllForTenantAsync()` + bellek-içi eşleme ile yapar; interface'e batch metot EKLENMEZ. `$in` batch = Faz 2. Gerekçe: Faz 1 self-contained + interface-değişmez; register küçük (bugün 0 satır).
- **Tek resolver ilkesi:** port da HTTP uç de tek `ResolveDocumentEffectivenessQuery` üstünde ince adaptör.
- **Ayrık dönüş tipi + fail-closed:** `Effective` / `Blocked(reason)` / `Unresolved`; repo istisnası ASLA Unresolved'a çevrilmez, yukarı fırlar.
- **Sessiz varsayılan YOK:** `By` çağıran tarafından açıkça verilir.

## §16.2 Waiver — W-EFF-BRANCH

```text
Scope:               Faz 1'in ayrı temiz Platform branch yerine feature/crm-integration-v2 üstünde açılması
Reason:              Owner kararı (DEC-A, 2026-09-04)
Owner/approver:      user (Control Tower üzerinden)
Compensating control: Faz 1 allowed-path'leri 165 dirty dosyayla çakışmıyor (ölçüldü); prompt'ta sıkı
                     "CRM dosyalarına dokunma" guard'ı; commit yalnız Faz 1 dosyalarını kapsayacak
Risk accepted:       commit hijyeni — Platform işi CRM dirty tree ile aynı branch'te yaşar
Review/expiry:       Faz 1 commit'inde gözden geçir; ideal olarak kendi commit'inde izole tut
Exit condition:      Faz 1 kendi atomik commit'iyle işaretlenince waiver kapanır
```

## Acceptance / Evidence

Required evidence level: **E2** (build + birim testleri, vacuity dahil). Runtime (E3+) bu fazda yok.
Kabul, aşağıdaki §36.1 prompt'unun DOĞRULA bloğundaki tüm testler YEŞİL + build temiz olunca CT tarafından verilir. Agent PASS ≠ CT ACCEPTED (K13).

---

## §37 Verification Report — CT bağımsız doğrulama (2026-09-04)

```text
WP ID:            WP-0029-EFFECTIVENESS-F1
Verifier:         Control Tower (bağımsız; implementer'dan ayrı)
Branch/HEAD:      feature/crm-integration-v2 @ 14825d44
Agent Verdict:        PASS (15/15 iddia)
Verification Verdict: PASS
CT Status:            ACCEPTED
Evidence achieved:    E2   ·  Required: E2
```

Checks (ölçülmüş, rapora güvenilmedi — K2/K13):
- **Scope:** commit 14825d44 = 7 dosya +448/−0, salt-ekleme; izin-dışı/CRM dosyası YOK (git ile doğrulandı); base HEAD 579ba446 eşleşti; 165 dirty tree korundu.
- **Build:** izole worktree'de (C:/Users/user/w-eff @ commit) TEMİZ derlendi. (Ana ağaçta build, çalışan Platform.API PID 19276'nın DLL kilidi yüzünden bloklandı — kod hatası DEĞİL; bu yüzden izole worktree kullanıldı.)
- **Tests:** CT'nin kendi koşumu → **Başarısız: 0, Başarılı: 15, Toplam: 15** (136 ms).
- **Vacuity (K3):** fail-closed testi `ThrowingRegisterRepository` ile `TimeoutException` propagate'ini asserte ediyor — handler istisnayı yutsaydı RED olurdu (kod: register okuması try/catch'siz, handler L37). by=Uid/by=Code testleri decoy satırla yanlış-alan eşleşmesini reddediyor. Vacuity gerçek.
- **Deviations:** (1) ayrı FluentValidation dosyası yok — Faz 1 boş/whitespace'i handler'da eliyor, tam 400 reddi Faz 3; kapsam-içi. (2) `TenantGuard.RequireTenant` eklenmiş — kardeş GetSummaryAsync ile aynı, muhafazakâr fail-closed önkoşul, scope genişletmesi değil. İkisi de KABUL.
- **Not measured:** tam 3541-test regresyonunu CT kendi koşmadı (ajan 33→33 iddiası, E0); Faz 1 salt-ekleme olduğu ve mevcut 33 hata bilinen live-Mongo/FU08-11 engine testleri olduğu için bu faz için gerekli görülmedi.

## §30.1 Closure record

```text
WP/Prompt: WP-0029-EFFECTIVENESS-F1 / P-EFF v1.0
Agent verdict: PASS · Verification verdict: PASS · CT status: ACCEPTED
Branch/commit: feature/crm-integration-v2 @ 14825d44
What changed: tek resolver (query+handler) + DTO/enum + in-process port + DI + 15 test; salt-ekleme
Decisions honored: D-FAZ1-REPO (GetAllForTenantAsync + in-memory, interface değişmedi); no silent By default; fail-closed tipe gömülü; gerçek klasör düzeltmesi
Waiver: W-EFF-BRANCH — commit izole olduğu için karşılanmış sayılır (kapatıldı)
Intentionally not done: Faz 2 repo $in batch · Faz 3 HTTP · Faz 4 permission+seed · Faz 5/Adım 0 register tohumu
Evidence: git show 14825d44 · izole worktree build TEMİZ · dotnet test 15/15
Known gaps: DCP-005 hâlâ draft; register bugün 0 kayıt (Adım 0 ön koşulu Faz 2–3 canlı için)
Next work: Faz 2 (repo $in batch) — aynı feature klasörü ⇒ single-writer, aynı lane
```

---

## §36.1 Agent Prompt (paste-ready)

```text
## Agent Prompt

@[.antigravity/agents/backend-architect.md]
WP: WP-0029-EFFECTIVENESS-F1 · Prompt P-EFF v1.0

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/crm-integration-v2 · Expected HEAD: 579ba446 · Worktree: ana checkout
⚠ Bu branch'te 165 dirty dosya (ağırlıkla CRM) var. Onlara DOKUNMA. Yalnız aşağıdaki allowed
  path'lerde yeni dosya/edit yap; commit'in yalnız Faz 1 dosyalarını içersin.

Önce oku (sırayla):
1. execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-contract-v2.md   (SÖZLEŞME — bağlayıcı)
2. execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-work-plan-adim0-adim1.md   (iş planı)
3. AGENTS.md · .antigravity/rules/**

Hedef servis: Diten.Platform · Proje: services/Diten.Platform/src/Diten.Platform.Application
Gerçek klasör (ÖLÇÜLDÜ — plandaki 'DocumentManagement/MasterRegister' YANLIŞ):
  services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentManagementMasterRegister/
Yeni entity YOK. Okunan: DocumentMasterRegisterEntry (LifecycleStatus, PermanentUid, DocumentCode).
Query: ResolveDocumentEffectivenessQuery → Response<DocumentEffectivenessResult>
Validator: yalnız boş/whitespace identifier ele (write yok; tam istek doğrulaması Faz 3).
Permission: YOK bu fazda (port in-process iş kuralı; RBAC Faz 4). Response<T> + MediatR kullan.

NE:  Tek resolver + DTO/enum + in-process port + DI + birim testleri. TEK COMMIT.
     - Queries/ResolveDocumentEffectivenessQuery.cs:
         record ResolveDocumentEffectivenessQuery(IReadOnlyList<string> Identifiers,
           DocumentIdentifierKind By, string CorrelationId) : IRequest<Response<DocumentEffectivenessResult>>
     - Handlers/QueryHandlers/ResolveDocumentEffectivenessHandler.cs
     - Models: DocumentEffectivenessResult { IReadOnlyList<DocumentEffectivenessItem> Items },
         DocumentEffectivenessItem(Identifier, State, DocumentCode?, PermanentUid?, LifecycleStatus?, BlockedReason?),
         enum DocumentEffectivenessState { Effective, Blocked, Unresolved },
         enum DocumentIdentifierKind { Code, Uid }
     - Services/IControlledDocumentEffectivenessPort.cs + ControlledDocumentEffectivenessPort.cs
         (IMediator'ı sarar; port record DocumentEffectivenessQuery(Identifiers, By) — CorrelationId'siz)
     - DI (DependencyInjection.cs): port AddScoped; handler MediatR otomatik.
NEDEN: Görev Merkezi'nin (MOD-0024) görev-türü etkinleştirmesi yürürlüğü canlı Master Register'dan
     (MOD-0029-FU06) okumalı; bugün CSV kopyasına bakıyor. DCP-005 doküman-yönetimi tarafı, Faz 1.
NASIL:
     - Çözümleme: IDocumentMasterRegisterRepository.GetByPermanentUidsAsync/GetByDocumentCodesAsync
       HENÜZ YOK. Faz 1'de repo'nun MEVCUT GetAllForTenantAsync() ile çek + BELLEK-İÇİ eşle
       (register küçük). Interface'e DOKUNMA — batch $in Faz 2. [Karar D-FAZ1-REPO, onaylı.]
     - Her identifier → satır bulunduysa: IsOperationallyEffective() ? Effective : Blocked;
       BlockedReason = LifecycleStatus.ToString(). Satır yoksa → Unresolved.
     - By=Code → DocumentCode'dan; By=Uid → PermanentUid'den eşle.
     - FAIL-CLOSED (tipe göm): repo ISTISNASI YAKALANMAZ, yukarı fırlar; ASLA Unresolved'a çevrilme.
       Unresolved yalnız "kayıt yok" veri gerçeği; altyapı hatası değil.
     - SESSIZ VARSAYILAN YOK: By açıkça verilir.
     Persistence: no writes. Consistency: N/A (read).
YAPMA:
     - Interface'e batch metot EKLEME (Faz 2). HTTP controller/route AÇMA (Faz 3).
     - Permission sabiti/seed EKLEME (Faz 4). Register'a veri YAZMA (Faz 5/Adım 0).
     - CSV / DocumentReferenceEntry / Tasks / mevcut 2 donmuş satır — DOKUNMA.
     - 165 dirty CRM dosyasına DOKUNMA. Eksik contract/tip UYDURMA.
DOĞRULA (E2 — birim testleri):
     - Effective: LifecycleStatus ∈ {Effective, UnderRevision} → State=Effective
     - Blocked: diğer 7 üye (Draft, InReview, ApprovedPendingEffective, Suspended, Superseded,
       Retired, ObsoleteCopy) → State=Blocked, BlockedReason=durum adı
     - Unresolved: identifier hiçbir satıra çözülmedi
     - by=Code ve by=Uid ayrı ayrı doğru alandan çözer
     - Repo istisna atar → PROPAGATE (Unresolved'a ÇEVRİLMEZ) — fail-closed kanıtı
     - Karışık batch (Effective+Blocked+Unresolved) → her öğe kendi dalında
     - Port contract: port, aynı girdi için query ile AYNI sonucu döndürür (tek resolver kanıtı)
     - `dotnet test` ilgili proje YEŞİL; build temiz.

Durma koşulları: contract eksik · ownership conflict · protected-path ihtiyacı · branch/HEAD mismatch ·
beklenmedik migration. Kapsamı kendin genişletme (Faz 2–5'e geçme); dur ve raporla.

Rapor formatı: §22 structured report. Senin PASS'in kapanış değildir (K13).
```
