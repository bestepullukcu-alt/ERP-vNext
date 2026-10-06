# WORK PACKAGE — WP-CL-CFG-1 · MLR onay şablonları (canlı) + kanıt yetkisi grant script'i — tenant 97c5 (tarayıcı/API + script, uygulama KODU YOK)

> **CT (SoR).** İddialar v2 Faz 4'ün öne çekilen kısmı. BE-4 onay akışının canlıda çalışması için gereken **konfigürasyon**.
>
> **Ön koşul:** fleet restart yapılmış olmalı. Canlıda şunlar çalışıyor olmalı:
> - BE-3a doğrulayıcı düzeltmesi (boş aday listesi kabul ediliyor);
> - BE-4 CRM tüketicisi.
>
> **Veri yazma yetkisi (kullanıcı verdi, yalnız bu liste):**
> 1. 7 workflow şablonunun oluşturulması ve yayınlanması (tenant 97c5).
> 2. `scripts/rbac/grant_claims_v2_rbac_97c5.py` script'inin **yazılması** (dry-run varsayılan). Script'i **kullanıcı** `--apply` ile çalıştırır. **Agent RBAC'a YAZMAZ.**
>
> **Çalışma yeri:** worktree `C:\tmp\cl-cfg-1`, dal `wp/cl-cfg-1`. Commit (script + WP raporu) bu dala, push YOK. Canlı işlemler çalışan fleet'e (localhost:5001 / 5000) yapılır.

## Kanıt (CT canlı salt-okuma, 2026-09-28)
- **Pozisyonlar** (WP-ORG-02, Active):

  | Pozisyon | Ad | Id |
  |---|---|---|
  | POS-MED-DIR | Medikal Direktör | `d0b6ce33-09ab-4789-bfaa-e49d53880fef` |
  | POS-MED-SPC | Medikal Uzman | `5fd3988e-433a-4297-9085-eea42acef5b8` |
  | POS-LEG-CNS | Hukuk Müşaviri | `7a72bd14-9112-4fd8-83d2-ca80d6ab9f81` |
  | POS-LEG-SPC | Hukuk Uzmanı | `4967a03c-01ad-4909-8609-bc672e5fa51b` |
  | POS-REG-MGR | Ruhsat Müdürü | `b122780a-1625-4a70-91ff-713fc2778111` |
  | POS-REG-SPC | Ruhsat Uzmanı | `0f0b5c2c-c67f-42a7-a100-733e1fe784fc` |

  Atama: sema pullukcu → MED-DIR (Primary), LEG-CNS ve REG-MGR (Secondary).
- **Tanım JSON biçimi** (canlıdaki mevcut şablondan):
  ```json
  {"schemaVersion":"workflow_schema_v1","expressionVersion":"workflow_expr_v1","requestedObjectType":"…",
   "stages":[{"code":"stage-1","name":"…","steps":[
     {"code":"step-1","name":"…","type":"approval",
      "assignment":{"mode":"candidate_principals","candidatePrincipalIds":["position:<guid>"]},
      "requirements":{"commentRequired":false,"evidenceRequired":false},
      "sla":{"dueInMinutes":…,"escalateAfterMinutes":…,"escalationPrincipalIds":["position:<guid>"]}}]}]}
  ```
  Ayrıştırıcı: `Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowDefinitionRuntimePlan.cs`.
- **Ekran:** `/Platform/Workflow` tenant kullanıcılarına açık (`ShellAccessFilter` MOD-0023 istisnası). Aynı-origin proxy: `/Platform/Workflow/api/{**everything}`.
- **API:** `POST api/v1/workflow/definitions` → `POST definitions/{id}/publish`.
- **97c5 Admin rolü:** hem Admin User hem sema pullukcu'da var. Rolde şunlar **mevcut**: `crm.claim.read/manage/approve`, `platform.workflow.*` (definitions / instances / tasks), `platform.document-management.controlled-documents.view`. **Eksik:** `platform.evidence.links.read`, `platform.evidence.links.manage`, `platform.document-management.external-documents.view` (üçü de PlatformAdmin kapsamlı).
- **Grant script deseni:** `scripts/rbac/grant_planned_visit_rbac_97c5.py`:
  - dry-run varsayılan;
  - GUID'ler **BSON subtype-4**;
  - tarih alanları yerel bir 97c5 rolePermissions dokümanından **aynen kopyalanır**;
  - rol ve izin oluşturmaz;
  - idempotent;
  - marker ile rollback yapılabilir.

## NE

### 1. 7 onay şablonu (canlı, UI ya da aynı-origin proxy üzerinden)
| TemplateCode | Ad | Kullanım |
|---|---|---|
| `CLAIM-CORE-MLR` | İddia — Global çekirdek MLR onayı | çekirdek iddia |
| `CLAIM-LOCAL-MLR-TR` | İddia — Türkiye yerel MLR onayı | TR ülke sürümü / TR yerel iddia |
| `CLAIM-LOCAL-MLR-BY` | … Belarus | BY |
| `CLAIM-LOCAL-MLR-UZ` | … Özbekistan | UZ |
| `CLAIM-LOCAL-MLR-TM` | … Türkmenistan | TM |
| `CLAIM-LOCAL-MLR-GE` | … Gürcistan | GE |
| `CLAIM-LOCAL-MLR-AZ` | … Azerbaycan | AZ |

**Her şablon: tek aşama, 3 sıralı adım.** Kullanıcı kararı: sıralı onay; MLR ekibi yalnız TR'de ve tüm ülkeler aynı pozisyonları kullanıyor.

| Adım | code | Adaylar | Gereklilik | SLA |
|---|---|---|---|---|
| 1 Medikal inceleme | `medical` | POS-MED-DIR, POS-MED-SPC | yorum **zorunlu değil** | dueInMinutes 4320 (3 gün), escalateAfterMinutes 2880 → POS-MED-DIR |
| 2 Hukuk inceleme | `legal` | POS-LEG-CNS, POS-LEG-SPC | aynı | aynı → POS-LEG-CNS |
| 3 Ruhsat inceleme | `regulatory` | POS-REG-MGR, POS-REG-SPC | aynı | aynı → POS-REG-MGR |

- `requestedObjectType`: çekirdek için `crm.claim`, yerel şablonlar için `crm.claim-country-version`.
- Alan zorunluysa `stages[0].code` = `mlr`, name `MLR İnceleme`.
- Ret gerekçesinin zorunluluğu motorun kendi kuralıdır; şablonda ayrıca bir şey yapılmaz.
- Oluştur → **yayınla** (değişmez sürüm).
- **Idempotent:** aynı TemplateCode varsa oluşturma. Yayınlı değilse raporla, yeniden oluşturma.

### 2. Grant script'i (yaz, ÇALIŞTIRMA)
`scripts/rbac/grant_claims_v2_rbac_97c5.py`:
- Anahtarlar: `platform.evidence.links.read`, `platform.evidence.links.manage`, `platform.document-management.external-documents.view`.
- Hedef rol: `--role` parametresiyle (varsayılan `Admin`), tenant 97c5.
- Marker `manual-grant-claims-v2-rbac`. Güvenlik kuralları planned-visit script'inin **birebir aynısı**.
- `--apply` olmadan yalnız rapor verir.

### 3. Canlı salt-okuma doğrulaması
- Şablonlar: 7 şablon Published, adım/aday/SLA yapısı doğru. Workflow ekranında listeleniyor.
- Aday çözümü: `GET api/v1/workflow/lookup/positions` 6 pozisyonu dönüyor. **Instance başlatma YOK** (o, E2E testinin işi).
- Script dry-run çıktısı raporda.

## KORU / YAPMA
- **Uygulama kodu DEĞİŞMEZ.** Yalnız script dosyası + WP raporu commit edilir.
- **Mevcut şablonlara** (refef, purchase-request-approval…), pozisyonlara, atamalara, rollere DOKUNMA.
- **RBAC'a doğrudan yazma YOK.** Script'i kullanıcı çalıştırır.
- **Test instance'ı başlatma YOK.** İddia oluşturma YOK.
- Şifre ya da kimlik bilgisi GİRME. Oturum yoksa DUR. Kullanıcının oturumlu diğer sekmelerine dokunma, ayrı sekme kullan.
- **DUR:**
  - Tanım şeması SLA ya da `escalationPrincipalIds`'i reddediyorsa → SLA'sız oluştur ve raporla.
  - Yayın 4xx dönüyorsa → dur, gövdeyi raporla.
  - `/Platform/Workflow` tenant oturumunda 403 veriyorsa → dur ve raporla.

## Acceptance
- 7 şablon Published. Adım/aday/SLA ekran görüntüsü ya da API yanıtıyla gösterilir.
- Script dry-run çıktısı: 3 anahtar, Admin rolü, "would insert N" / "already granted".
- Kod diff YOK (yalnız `scripts/rbac/grant_claims_v2_rbac_97c5.py` + WP).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (fleet restart SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CL-CFG-1 · MLR onay şablonları (canlı) + kanıt yetkisi grant script'i — tenant 97c5 (uygulama KODU YOK)
Repository: C:\tmp\cl-cfg-1 (worktree; script+rapor commit) · Branch: wp/cl-cfg-1 · push YOK · canlı işlemler localhost:5001/5000 fleet'ine

Amaç: BE-4 iddia onay akışının canlıda çalışması için 7 MLR şablonunu (CLAIM-CORE-MLR + CLAIM-LOCAL-MLR-TR/BY/UZ/TM/GE/AZ; tek aşama, sıralı Medikal→Hukuk→Ruhsat, adaylar WP-ORG-02 pozisyonları) oluşturup yayınla; eksik kanıt yetkileri için grant script'ini YAZ (çalıştırma — kullanıcı çalıştırır).

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-CFG-1-mlr-workflow-templates-grants.md (pozisyon id'leri, JSON biçimi, adım/SLA tablosu, izinli yazmalar) · services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/Handlers/CommandHandlers/WorkflowDefinitionRuntimePlan.cs (şema) · frontend/Diten.Web/Controllers/WorkflowController.cs (/Platform/Workflow + api proxy) · scripts/rbac/grant_planned_visit_rbac_97c5.py (script deseni) · memory mongo-guid-subtype-write-recipe, rolepermission-guid-subtype-login-500.

Ön koşul: oturum açık mı (97c5); değilse DUR, kullanıcıdan tarayıcıda giriş yapmasını iste — şifre GİRME.

YAP:
 1) 7 şablon (yoksa oluştur → yayınla): tek aşama; adımlar medical [position:d0b6ce33-09ab-4789-bfaa-e49d53880fef, position:5fd3988e-433a-4297-9085-eea42acef5b8] → legal [position:7a72bd14-9112-4fd8-83d2-ca80d6ab9f81, position:4967a03c-01ad-4909-8609-bc672e5fa51b] → regulatory [position:b122780a-1625-4a70-91ff-713fc2778111, position:0f0b5c2c-c67f-42a7-a100-733e1fe784fc]; commentRequired false; sla dueInMinutes 4320, escalateAfterMinutes 2880, escalationPrincipalIds = o adımın yönetici pozisyonu; requestedObjectType crm.claim (çekirdek) / crm.claim-country-version (yerel). Aynı kod varsa oluşturma, raporla.
 2) scripts/rbac/grant_claims_v2_rbac_97c5.py YAZ: anahtarlar platform.evidence.links.read, platform.evidence.links.manage, platform.document-management.external-documents.view; --role (varsayılan Admin) tenant 97c5; marker manual-grant-claims-v2-rbac; planned-visit script'inin güvenlik kuralları birebir (subtype-4 GUID, tarih alanları yerel dokümandan kopya, rol/izin oluşturmaz, idempotent, dry-run varsayılan). Dry-run'ı çalıştır (yalnız okuma), çıktıyı rapora koy. --apply ÇALIŞTIRMA.
 3) Canlı salt-okuma: 7 şablon Published + adım/aday/SLA; lookup/positions 6 pozisyon.
KORU/YAPMA: uygulama kodu DEĞİŞMEZ; mevcut şablon/pozisyon/atama/rol DOKUNMA; RBAC'a doğrudan yazma YOK; test instance başlatma ve iddia oluşturma YOK; kullanıcının oturumlu sekmelerine dokunma, ayrı sekme.
Durma: şema SLA/escalation'ı reddederse SLA'sız oluştur+raporla; yayın 4xx → DUR+gövde; /Platform/Workflow tenant'ta 403 → DUR.
RAPOR (§22 TÜRKÇE): 7 şablon tablosu (kod, id, sürüm, durum, adım sayısı); oluşturulan JSON örneği (bir şablon); script dry-run çıktısı; ekran görüntüsü; konsol/ağ hataları. Commit (script + WP'ye rapor özeti): "chore(rbac): WP-CL-CFG-1 — claims v2 MLR workflow templates (live) + evidence grant script (dry-run)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. K13.
```

---

## §36.2 Uygulama raporu (owner, 2026-09-28) — canlı 97c5

**Ön koşul:** oturum açıktı (yerleşik tarayıcı, ayrı sekme). `/Platform/Workflow` 200 döndü, 403 yok. Listede yalnız `refef` ve `purchase-request-approval` vardı; aynı kodlu şablon yoktu. Mevcut şablonlara dokunulmadı.

### 1. Şablonlar — 7/7 Published
Oluşturma `POST definitions` → yayın `POST definitions/{id}/publish`; aynı-origin proxy `/Platform/Workflow/api/…` üzerinden. Hepsi `201 → 200`. Şema SLA ve `escalationPrincipalIds`'i kabul etti.

| Kod | Template id | Aktif sürüm (v1) | Durum | Aşama / adım | requestedObjectType |
|---|---|---|---|---|---|
| `CLAIM-CORE-MLR` | `10cac2a6-648a-42a7-acf3-594be88a2f5d` | `6f4d1c3d-5c4f-4cbb-9682-943e5a0a633b` | Published, değişmez | 1 / 3 | `crm.claim` |
| `CLAIM-LOCAL-MLR-TR` | `83c44fe7-8670-4ff5-b7c0-bf1604f9441b` | `8f5e5504-4c76-4923-9c64-0702e2232a64` | Published, değişmez | 1 / 3 | `crm.claim-country-version` |
| `CLAIM-LOCAL-MLR-BY` | `7e14f6bc-e68a-4468-9101-ee4347a6441b` | `9f151b56-12a3-46e8-8a3c-3a21a845f7cb` | Published, değişmez | 1 / 3 | `crm.claim-country-version` |
| `CLAIM-LOCAL-MLR-UZ` | `c8532f8f-f9cb-4111-8ed4-5595c019312a` | `b6a5d0fd-3f28-45e6-ae10-5e68069faca4` | Published, değişmez | 1 / 3 | `crm.claim-country-version` |
| `CLAIM-LOCAL-MLR-TM` | `0985d1ce-a0ea-40f6-ae94-81634cb54776` | `3519276a-773d-4408-b463-4064e9cade18` | Published, değişmez | 1 / 3 | `crm.claim-country-version` |
| `CLAIM-LOCAL-MLR-GE` | `b0c2e90a-e58d-4aa0-9960-8d2e50231da5` | `7dc04477-3ef2-4b08-9428-226d96f094ef` | Published, değişmez | 1 / 3 | `crm.claim-country-version` |
| `CLAIM-LOCAL-MLR-AZ` | `eef8a63d-3b73-4f05-b991-a2dd6c4b7078` | `1e4c814c-a1a4-42e3-af3f-92897a498e08` | Published, değişmez | 1 / 3 | `crm.claim-country-version` |

Salt-okuma doğrulaması: her şablonun detayındaki `activePublishedVersionId`, yayınlı v1 ile aynı. Yayınlı JSON'dan okunan yapı 7 şablonda aynı:
`medical[MED-DIR, MED-SPC] 4320/2880 → MED-DIR` → `legal[LEG-CNS, LEG-SPC] 4320/2880 → LEG-CNS` → `regulatory[REG-MGR, REG-SPC] 4320/2880 → REG-MGR`.
`lookup/positions` 200 döndü (23 pozisyon); 6 MLR pozisyonunun hepsi listede ve `Active`.

Not: `GET definitions` liste DTO'su `activePublishedVersionId` taşımıyor (alanlar id / templateCode / name / status / createdAt). Aktif sürüm detay ucundan okunur.

**Oluşturulan JSON (CLAIM-CORE-MLR; yereller yalnız `requestedObjectType` ile farklı):**
```json
{"schemaVersion":"workflow_schema_v1","expressionVersion":"workflow_expr_v1","requestedObjectType":"crm.claim",
 "stages":[{"code":"mlr","name":"MLR İnceleme","steps":[
  {"code":"medical","name":"Medikal inceleme","type":"approval",
   "assignment":{"mode":"candidate_principals","candidatePrincipalIds":["position:d0b6ce33-09ab-4789-bfaa-e49d53880fef","position:5fd3988e-433a-4297-9085-eea42acef5b8"]},
   "requirements":{"commentRequired":false,"evidenceRequired":false},
   "sla":{"dueInMinutes":4320,"escalateAfterMinutes":2880,"escalationPrincipalIds":["position:d0b6ce33-09ab-4789-bfaa-e49d53880fef"]}},
  {"code":"legal","name":"Hukuk inceleme","type":"approval",
   "assignment":{"mode":"candidate_principals","candidatePrincipalIds":["position:7a72bd14-9112-4fd8-83d2-ca80d6ab9f81","position:4967a03c-01ad-4909-8609-bc672e5fa51b"]},
   "requirements":{"commentRequired":false,"evidenceRequired":false},
   "sla":{"dueInMinutes":4320,"escalateAfterMinutes":2880,"escalationPrincipalIds":["position:7a72bd14-9112-4fd8-83d2-ca80d6ab9f81"]}},
  {"code":"regulatory","name":"Ruhsat inceleme","type":"approval",
   "assignment":{"mode":"candidate_principals","candidatePrincipalIds":["position:b122780a-1625-4a70-91ff-713fc2778111","position:0f0b5c2c-c67f-42a7-a100-733e1fe784fc"]},
   "requirements":{"commentRequired":false,"evidenceRequired":false},
   "sla":{"dueInMinutes":4320,"escalateAfterMinutes":2880,"escalationPrincipalIds":["position:b122780a-1625-4a70-91ff-713fc2778111"]}}]}]}
```
Yayın isteği: `schemaVersion: "workflow_schema_v1"`, `expressionVersion: "workflow_expr_v1"`, `publishReason` = WP-CL-CFG-1. Yayınlayan: oturumdaki kullanıcı.

### 2. Grant script'i — yazıldı, yalnız dry-run çalıştırıldı
`scripts/rbac/grant_claims_v2_rbac_97c5.py`. `grant_planned_visit_rbac_97c5.py` ile gövde farkı yalnız 3 satır: marker, anahtar listesi, `--role` varsayılanı (`Admin`). Güvenlik mantığı birebir aynı.
```
> py -3 scripts/rbac/grant_claims_v2_rbac_97c5.py
role 'Admin' (6a315467-7d80-4ad8-bd76-78f8f762fe8a): 3 grant(s) to insert: ['platform.document-management.external-documents.view', 'platform.evidence.links.manage', 'platform.evidence.links.read']
DRY-RUN — nothing written. Re-run with --apply.
```
Sonrasında `rolePermissions` içinde `CreatedBy: manual-grant-claims-v2-rbac` satırı sayısı **0**; hiçbir şey yazılmadı.
**Kullanıcı adımı:** `py -3 scripts/rbac/grant_claims_v2_rbac_97c5.py --apply`, ardından Admin rolündeki kullanıcılar yeniden giriş yapar.

### 3. Hata / sapma
- Konsol hatası yok. Ağda 4xx/5xx yok.
- DUR koşullarının hiçbiri tetiklenmedi.
- Instance başlatılmadı, iddia oluşturulmadı. RBAC'a, mevcut şablonlara, pozisyonlara ve atamalara yazılmadı. Uygulama kodu değişmedi.

## §37 CT bağımsız doğrulama (2026-09-28) → **ACCEPTED (canlı)**
```
Commit: d62ba56a · CT: canlı DB salt-okuma (diten_personalization_dev + diten_auth_v3)
```
- ✅ **7 şablon (97c5):**
  - Şablonlar: CLAIM-CORE-MLR + CLAIM-LOCAL-MLR-TR / BY / UZ / TM / GE / AZ.
  - Şablon Status 2, sürüm 1 **Published + IsImmutable**.
  - Adımlar `medical → legal → regulatory`, her adımda **2 aday**, `sla.dueInMinutes 4320`.
  - `requestedObjectType`: çekirdek `crm.claim`, yerel `crm.claim-country-version`.
- ✅ **Grant uygulandı** (kullanıcı onayıyla, script `--apply`). Marker `manual-grant-claims-v2-rbac` **3 satır**, hepsi 97c5 **Admin** rolünde: `platform.evidence.links.read/manage`, `platform.document-management.external-documents.view`.
  - `_id`, TenantId, RoleId, PermissionId → **subtype 4**.
  - CreatedAt dizi, AssignedAt DateTime (yerel dokümanlarla aynı).
  - IsDeleted false.
- ✅ **Kapsam:** yalnız `scripts/rbac/grant_claims_v2_rbac_97c5.py` + bu WP. Uygulama kodu diff YOK.
- ⏳ **Kullanıcı:** Admin User ve sema pullukcu **çıkış yapıp tekrar girmeli** (yetkiler JWT'ye giriş sırasında yazılıyor).
- **Rollback:** `db.rolePermissions.deleteMany({CreatedBy:"manual-grant-claims-v2-rbac"})`.
