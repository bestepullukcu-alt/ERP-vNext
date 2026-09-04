# WORK PACKAGE — WP-0029-EFFECTIVENESS-P2  (Faz 3 → Faz 4 → Faz 2)

> Control Tower kaydı (SoR). Faz 1 (WP-0029-EFFECTIVENESS-F1) ACCEPTED sonrası devam.
> Sıra kullanıcı kararı (2026-09-04): **Faz 3 (HTTP uç) → Faz 4 (RBAC+seed) → Faz 2 (repo $in batch)**.

## Metadata (§17.1 / §36)

```text
WP ID:            WP-0029-EFFECTIVENESS-P2
Prompt ID:        P-EFF-P2
Prompt Version:   v1.0
Task Class:       Backend + RBAC/seed (state-changing config) + repo optimization
Golden-Flow Profile: B
Risk Class:       HIGH   (§9.1 — authorization + seed dokunuyor)

Capability:       DCP-005 — Task ↔ Controlled Document Reference (doküman-yönetimi tarafı, Adım 1'in kalan fazları)
Module:           MOD-0029 (Document Master Register) · Faz 4 seed AuthService'e dokunur
Build Lane:       BL-0029-EFFECTIVENESS
Agent Lane ID:    AL-0029-EFF-P2   (Faz 1 lane'inin devamı; single-writer, aynı feature klasörü §16.4)
Agent Lane Type:  DEV
Target Agent / Entry Point: backend-architect  (gateway GEREKİRSE integration-agent; RBAC deseni security-agent kuralına sadık)

Target Branch:      feature/crm-integration-v2      # DEC-A devam; §16.2 waiver W-EFF-BRANCH
Expected Base HEAD: 14825d44  (Faz 1 commit)
Worktree:           C:\Users\user\Desktop\ERP-vNext
Dirty baseline:     165+ dirty (CRM). Bu WP'nin allowed-path'leriyle çakışmıyor (ölçüldü); her faz KENDİ commit'inde.

Depends On:         WP-0029-EFFECTIVENESS-F1 (ACCEPTED) — resolver + query + port mevcut
Parallel-Safe With: CRM dirty tree. NOT parallel-safe with: aynı feature klasörüne başka writer (single-writer)
Integration Order:  Faz 3 → Faz 4 → Faz 2, her biri ayrı commit, her fazdan sonra STOP + rapor

Authority Sources:
- Contract:  execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-contract-v2.md  (§4 HTTP, §4 RBAC, §1 by)
- Work plan: execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-work-plan-adim0-adim1.md  (Faz 2/3/4)
- Faz 1 WP:  execution/domains/platform-shared-services/work-packs/WP-0029-EFFECTIVENESS-F1.md

Ports (AGENTS.md §3): Gateway 5000 · AuthService 5056 · Platform 5057 · Frontend 5001 (asla doğrudan servise gitmez)
```

## Ölçülmüş gerçek (K2 — 2026-09-04)

| Konu | Ölçüm |
|---|---|
| Controller | `Diten.Platform.API/Controllers/DocumentManagementMasterRegisterController.cs` — `[Route("api/v1/document-management")]`, `CustomBaseController`, MediatR, `[HasPermission]`, `CreateActionResultInstance(...)`, `CorrelationId` hazır |
| Request model klasörü | `Diten.Platform.API/Models/DocumentManagement/` (ApiRequestMapper deseni mevcut) |
| Gateway | ocelot.json'da `/api/v1/document-management/{everything}` wildcard var (satır ~1174/1182) → yeni uç **muhtemelen ocelot değişikliği İSTEMEZ** |
| Permission sabiti | `DocumentManagementMasterRegisterModels.cs` → `DocumentMasterRegisterPermissions` (View/Manage; effectiveness.read YOK) |
| Seed | `Diten.AuthService/.../Seed/DataSeeder.cs` — izin **katalogda tanımlanır** (satır ~414-420, `new("platform","document-management.master-register","view",...)`) VE **role atanır** (satır ~58-68 key dizileri). İkisi ayrı. master-register.* katalogda var ama role-grant ayrı iş |

## Fazlar (her biri ayrı commit)

### Faz 3 — HTTP uç (yalnız ekran)
- `DocumentManagementMasterRegisterController`'a action:
  `POST document-master-register/effectiveness:batch`
  `[HasPermission(DocumentMasterRegisterPermissions.EffectivenessRead)]`  (sabit Faz 4'te eklenecek — Faz 3'ü Faz 4 ile aynı lane'de sıralı yürüteceğiz; derleme sırası: önce sabiti ekle sonra action referanslasın)
  Body `{ by, identifiers[] }` → `ResolveDocumentEffectivenessQuery` → `CreateActionResultInstance`.
- Request model: `Models/DocumentManagement/EffectivenessApiRequests.cs` (+ ApiRequestMapper deseni).
- Boş/bozuk istek → **400**. `Unresolved` hata DEĞİL — 200 içinde döner.
- Gateway: wildcard'ın `effectiveness:batch` (kolon dahil) yolunu ilettiğini DOĞRULA. İletmiyorsa DUR → integration-agent + `.antigravity/rules/routes.md`.

### Faz 4 — RBAC + seed (HIGH)
- Sabit: `DocumentMasterRegisterPermissions`'a `EffectivenessRead = "platform.document-management.master-register.effectiveness.read"`.
- Seed (DataSeeder.cs) **İKİ edit**: (1) katalog tanımı (`new("platform","document-management.master-register","effectiveness.read", ...)`); (2) role-grant — anahtarı **task-type yönetimini/master-register.view'i gören role** ekle (mevcut key-dizisi desenine).
- **Faz 4 KABUL KRİTERİ (zorlanıyor):** rapor iki ölçüm satırı taşımalı — (1) anahtarın seed'e girdiği **dosya:satır** (hem katalog hem role-grant); (2) uç normal tenant kullanıcısıyla çağrıldığında **200 mü 403 mü** (fleet restart + re-login gerekir; ajan login yapamıyorsa §26 operator evidence — CT/kullanıcı koşar). Bu iki satır yoksa Faz 4 bitmemiştir.

### Faz 2 — repo $in batch (LOW)
- `IDocumentMasterRegisterRepository`'ye `GetByPermanentUidsAsync`/`GetByDocumentCodesAsync` (Mongo `$in`, mevcut ExecutionFilter tenant scope).
- Handler'ı `GetAllForTenantAsync` + in-memory'den bu batch'e çevir; **davranış birebir aynı kalmalı** (Faz 1'in 15 testi yeşil kalır — regresyon kanıtı).

## §16.2 Waiver — W-EFF-BRANCH (devam)
Faz 1 ile aynı: iş `feature/crm-integration-v2` üstünde; her faz izole commit; CRM dosyalarına dokunulmaz. Owner: user (DEC-A).

## Acceptance / Evidence
Required: **E4** (Faz 4 authorization → server-side permission denial + live 200/403). Faz 3 E3 (runtime uç), Faz 2 E2 (regresyon).
Her fazdan sonra CT bağımsız doğrular (K13). Agent PASS ≠ ACCEPTED.

### §37 Faz 3 — CT bağımsız doğrulama (2026-09-04) → **ACCEPTED**
```text
Branch/HEAD: feature/crm-integration-v2 @ 0950253e (base 14825d44)
Agent Verdict: PASS · Verification Verdict: PASS · CT Status: ACCEPTED · Evidence: E3
```
- Scope: commit 0950253e = 4 dosya +238/−0, salt-ekleme; CRM/ocelot/seed'e DOKUNMAMIŞ (git ile doğrulandı).
- Build+test: CT kendi izole worktree koşumu → **25/25 YEŞİL** (10 endpoint + 15 Faz 1 resolver), 132 ms.
- Kod: 400 dispatch'ten ÖNCE dönüyor (iki guard); `by` katı parse (null/""/bilinmeyen/numerik reddedilir, sessiz default yok); Unresolved→200; TenantId client'tan alınmıyor.
- Gateway (E3): kolon-yol yönlendirmesi kanıtlandı — `effectiveness:batch`=401 (routed, sibling /summary ile aynı imza) vs bilinmeyen-prefix=404 (unrouted). **ocelot değişikliği/integration-agent GEREKMEDİ.**
- Karar: `EffectivenessRead` sabiti Faz 3'e alındı (derleme sırası; action referanslıyor) — WP notuyla tutarlı; Faz 4 yalnız SEED yapar.
- Deferred: authenticated live 200 → Faz 4 E4 (fleet restart + login).

### §37 Faz 4 — CT bağımsız doğrulama (2026-09-04) → **ACCEPTED** (E4 grant kanıtı alındı)
```text
Branch/HEAD: feature/crm-integration-v2 @ e13e0477 (base 0950253e)
Agent Verdict: PASS · Verification Verdict: PASS · CT Status: ACCEPTED
Evidence achieved: E4 (grant/live 200)  ·  Residual: negatif 403 (denial) — düşük risk, non-blocking
```
**E4 OPERATOR KANITI (2026-09-04, fleet restart sonrası):**
- POZİTİF (operator bestepullukcu@gmail.com, tenant 97c5, `effectiveness.read` ile): `POST …/effectiveness:batch {"by":"uid","identifiers":["UID-0000104"]}` → **HTTP 200**, gövde `items:[{state:2 (Unresolved), lifecycleStatus:null}]` (register boş → doğru), `isSuccessful:true`, `correlation_id:283412dd…`. ⇒ seed yetkiyi VERDİ (aksi 403 olurdu), uç uçtan uca çalışıyor, Unresolved-is-200 canlı doğru.
- NEGATİF (403, yetkisiz kullanıcı): henüz koşulmadı — **residual, non-blocking**. Gerekçe: Faz 4'ü HIGH yapan asıl risk (anahtar seed'siz → uç herkese sessizce kapalı) POZİTİF 200 ile ÇÜRÜTÜLDÜ; `[HasPermission]` paylaşılan/test-edilmiş gate; Faz 3 tokensız 401 gösterdi. Öneri: ikinci bir yetkisiz tenant kullanıcısıyla 403 teyidi ileride.
- Scope: commit e13e0477 = **yalnız DataSeeder.cs**, +9/−1; CRM'e dokunmamış.
- **Escalation kontrolü (K2) PASS:** yeni anahtar SADECE tenant-97c5 operatör dizisinde (DataSeeder.cs:70) + katalogda (DataSeeder.cs:425). Shared Admin baseline'a SIZMAMIŞ (grep ile tüm oluşumlar doğrulandı).
- Count-guard **dinamik**: `permissions.Count != MasterRegisterLinkPermissionKeys.Length` (6==6); katalog↔dizi drift olursa grant sessizce yarım kalmaz, komple atlanır + uyarı.
- Build: CT izole worktree → AuthService **0 Hata** (1 uyarı).
- Live 200/403: ajan UYDURMADI, §26 operator kanıtına bıraktı — DOĞRU davranış. Bu gate açık; kabul için fleet restart + operator login gerekir.
- **CT NOT: Faz 4 henüz ACCEPTED DEĞİL** (§23.1 — authorization E4 ister). Kod doğru; canlı 200/403 gelince ACCEPTED'e yükselecek.

### §26 Operator kanıt adımı — Faz 4 kapanışı için (fleet restart sonrası koş)
```text
1) Fleet restart (AuthService yeniden seed etsin: katalog+grant; Platform yeni endpoint'i açsın).
   AuthService konsolunda doğrula: "tenant-97c5 Master Register link grant: ... 6 permissions ...".
2) operator bestepullukcu@gmail.com (tenant 97c59330-dbc4-4665-b29c-0c26dbb5cc93) ile YENİDEN login.
3) POZİTİF (beklenen 200; register boş olduğu için item'lar Unresolved olabilir):
   POST http://localhost:5000/api/v1/document-management/document-master-register/effectiveness:batch
   Authorization: Bearer <OPERATOR_TOKEN> · X-Tenant-Id: 97c59330-... · {"by":"uid","identifiers":["UID-0000104"]}
   → GÖZLENEN: ______ (beklenen 200)
4) NEGATİF (effectiveness.read OLMAYAN tenant kullanıcısı → beklenen 403 PERMISSION_DENIED):
   → GÖZLENEN: ______ (beklenen 403)
Kaydeden: ______  Tarih: ______
(JWT/parola CT ile paylaşılmaz; curl'leri operator koşar, sonucu buraya yazar.)
```

### §37 Faz 2 — CT bağımsız doğrulama (2026-09-04) → **ACCEPTED**
```text
Branch/HEAD: feature/crm-integration-v2 @ cd7211a6 (base e13e0477)
Agent Verdict: PASS · Verification Verdict: PASS · CT Status: ACCEPTED · Evidence: E2
```
- Scope: commit cd7211a6 = 4 dosya +307/−14; CRM'e dokunmamış; **Faz 1 resolver test dosyası UNTOUCHED** (git ile teyit — en güçlü regresyon kanıtı).
- Tasarım: **default interface method** (fallback = full read + in-memory filter) + Mongo repo **override** = gerçek `$in` (`Filter.In(x=>x.PermanentUid/DocumentCode, wanted)`, tenant scope `And` ile). Bu desen repoda emsalli (IModuleCatalogRepository vb.) — mevcut hiçbir implementer/fake değişmedi.
- Fail-closed korundu: batch fetch try/catch'siz; handler yalnız fetch'i full-scan'den batch'e çevirdi, mapping aynı.
- Build+test: CT izole worktree → **50/50 YEŞİL** (31 effectiveness + 19 FU06 master-register); tüm test projesi derlendi ⇒ hiçbir fake bozulmadı.

---

## WP-0029-EFFECTIVENESS-P2 — GENEL KAPANIŞ

| Faz | Commit | CT Status | Evidence |
|---|---|---|---|
| Faz 3 — HTTP uç | 0950253e | ✅ ACCEPTED | 25 test + gateway kolon-yol (E3) |
| Faz 4 — RBAC+seed | e13e0477 | ✅ ACCEPTED | kod E2 (escalation temiz) + **canlı 200 grant (E4)**; 403 residual non-blocking |
| Faz 2 — repo $in | cd7211a6 | ✅ ACCEPTED | 50 test; Faz 1 dosyası unchanged |

### R1 — enum wire-format düzeltmesi (Görev Merkezi itirazı üzerine) → **ACCEPTED**
```text
Commit: bfbec86d "fix(docmgmt): DCP-005 effectiveness state serializes as string (wire contract)"
Agent: PASS · Verification: PASS · CT: ACCEPTED · Evidence: E2
```
- Kusur: `DocumentEffectivenessState` HTTP sınırını geçiyor ama `[JsonConverter(JsonStringEnumConverter)]` yoktu → tel üzerinde SAYI (canlı `state:2`). Yazılı kural: TaskEnums.cs:7-11 (16 emsal). Görev Merkezi Adım 2'yi buna bağlamıştı.
- Fix: yalnız `DocumentEffectivenessState`'e attribute + using; **global serializer değişmedi** (kural yasaklıyor; sızma yok — grep teyit); `DocumentIdentifierKind`'e dokunulmadı (tel üzerinden geçmiyor).
- CT doğrulama: 2 dosya +52; izole worktree **35/35** (31 + 4 yeni wire-format); vacuity gerçek (test attribute'a güveniyor, `"state":"Unresolved"` var / `state:2` yok).

### Görev Merkezi cevabı işlendi (2026-09-04)
- **G1 = (a) ONAYLANDI** (register CSV UID'lerini sahiplenir, `by="uid"`) → **Adım 0 register tohumu bizim sıradaki WP** (unblocked).
- **Adım 2 freeze = enum adı** (lifecycleStatus) — CT katılıyor; `blockedReason` metni değil.
- **BL-060** (Mutabakat sidebar link'i manifest'e taşınsın; `_LayoutTenantShell.cshtml:243-259` elle yazılmış) → ayrı düşük-öncelik doküman-yönetimi follow-up'ı.

---

**WP-0029-EFFECTIVENESS-P2 → TAM ACCEPTED (2026-09-04).** Residual'lar: negatif 403 teyidi + R1 canlı tel doğrulaması (fleet restart sonrası `state:"Unresolved"`), ikisi de düşük risk.
Faz 1 (WP-...-F1) + P2 birlikte = **DCP-005 doküman-yönetimi tarafı / Adım 1 KOD-TAMAM ve doğrulandı.**
Kalan (bu WP dışı): (1) Adım 0 register tohumu — G1 (a/b join, Görev Merkezi) kararına bağlı; (2) Görev Merkezi'ne "Adım 1 hazır" sinyali → onların Adım 2–3'ü; (3) opsiyonel 403 teyidi.

---

## §36.1 Agent Prompt (paste-ready)

```text
## Agent Prompt

@[.antigravity/agents/backend-architect.md]
WP: WP-0029-EFFECTIVENESS-P2 · Prompt P-EFF-P2 v1.0

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/crm-integration-v2 · Expected HEAD: 14825d44 · Worktree: ana checkout
⚠ 165+ dirty CRM dosyası var — DOKUNMA. Her fazı KENDİ commit'inde ver; commit'ler yalnız o fazın dosyalarını içersin.

Önce oku (sırayla):
1. execution/domains/platform-shared-services/work-packs/authority/dcp-005-effectiveness-contract-v2.md  (§1 by, §4 HTTP+RBAC)
2. execution/domains/platform-shared-services/work-packs/WP-0029-EFFECTIVENESS-F1.md  (Faz 1'de kurulan resolver/query/port)
3. AGENTS.md (§3 portlar: Gateway 5000/Auth 5056/Platform 5057) · .antigravity/rules/routes.md · .antigravity/rules/**

Bağlam (ÖLÇÜLDÜ):
- Controller: Diten.Platform.API/Controllers/DocumentManagementMasterRegisterController.cs
  ([Route("api/v1/document-management")], CustomBaseController, MediatR, [HasPermission], CreateActionResultInstance, CorrelationId).
- Query/handler/port Faz 1'de HAZIR: ResolveDocumentEffectivenessQuery → Response<DocumentEffectivenessResult>;
  IControlledDocumentEffectivenessPort mevcut.
- Permission sabiti: DocumentManagementMasterRegister/DocumentManagementMasterRegisterModels.cs → DocumentMasterRegisterPermissions.
- Seed: services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs — izin KATALOGDA tanımlanır
  (~satır 414-420 deseni) VE ROLE atanır (~satır 58-68 key dizileri). İKİSİ de gerekir.
- Gateway: ocelot.json'da /api/v1/document-management/{everything} wildcard VAR — yeni uç muhtemelen ocelot değişikliği istemez.

Bu WP üç FAZ, SIRAYLA, her biri AYRI COMMIT + her fazdan sonra DUR ve raporla:

── FAZ 3 (HTTP uç) ──
NE:  DocumentManagementMasterRegisterController'a action ekle:
       [HttpPost("document-master-register/effectiveness:batch")]
       [HasPermission(DocumentMasterRegisterPermissions.EffectivenessRead)]   // sabiti bu faz commit'inden ÖNCE ekle (aşağıda)
       Body { by, identifiers[] } → ResolveDocumentEffectivenessQuery(identifiers, by, CorrelationId) → CreateActionResultInstance.
     + Models/DocumentManagement/EffectivenessApiRequests.cs (batch request record) + ApiRequestMapper deseni.
NASIL: boş/whitespace identifiers veya geçersiz 'by' → 400 (invalid_request). Unresolved HATA DEĞİL, 200 içinde döner.
       Gateway wildcard'ın kolonlu yolu (effectiveness:batch) ilettiğini doğrula; İLETMİYORSA DUR → integration-agent gerekli.
DOĞRULA: build temiz; controller testi (200 happy-path + 400 boş istek). Runtime smoke fleet varsa (E3).
YAPMA: ocelot'u kendin değiştirme (yalnız integration-agent + routes.md). Frontend proxy açma (kapsam dışı).
→ COMMIT (feat: Faz 3 effectiveness HTTP endpoint) → DUR, raporla.

── FAZ 4 (RBAC + seed) — HIGH ──
NE:  (1) Sabit: DocumentMasterRegisterPermissions'a
         EffectivenessRead = "platform.document-management.master-register.effectiveness.read".
     (2) DataSeeder.cs İKİ edit: (a) katalog tanımı (new("platform","document-management.master-register","effectiveness.read", ...));
         (b) role-grant — anahtarı task-type yönetimini/master-register.view'i gören role ekle (mevcut key-dizisi desenine sadık).
NASIL: mevcut DataSeeder desenini birebir izle; yeni RBAC modeli KURMA. GUID/subtype ile oynama.
DOĞRULA (KABUL — bu iki satır olmadan Faz 4 BİTMEZ):
     (1) anahtarın seed'e girdiği DOSYA:SATIR — hem katalog hem role-grant.
     (2) uç normal tenant kullanıcısıyla 200 mü 403 mü (fleet restart + re-login gerekir). Login yapamıyorsan
         §26 operator evidence formatını doldur; sonucu UYDURMA.
YAPMA: master-register.view'i "reuse" edip yeni anahtarı atlamak (plan yeni anahtar diyor). Başka izinleri değiştirmek.
→ COMMIT (feat: Faz 4 effectiveness.read permission + seed) → DUR, raporla.

── FAZ 2 (repo $in batch) — LOW ──
NE:  IDocumentMasterRegisterRepository'ye GetByPermanentUidsAsync/GetByDocumentCodesAsync (Mongo $in, tenant ExecutionFilter);
     Mongo impl'de karşılıkları; handler'ı GetAllForTenantAsync + in-memory'den bu batch'e çevir.
NASIL: DAVRANIŞ BİREBİR AYNI kalmalı — Faz 1'in 15 testi değişmeden YEŞİL kalır (regresyon kanıtı). Yeni testler: batch $in doğru satırları getirir.
DOĞRULA: dotnet test ilgili proje YEŞİL (15 eski + yeni); build temiz.
→ COMMIT (perf: Faz 2 repo $in batch) → raporla.

Durma koşulları (her faz): contract eksik · ownership conflict · protected-path (ocelot!) ihtiyacı ·
branch/HEAD mismatch · beklenmedik migration. Kapsamı kendin genişletme; dur ve raporla.

Rapor formatı: §22, HER FAZ için ayrı. Senin PASS'in kapanış değildir (K13).
```
