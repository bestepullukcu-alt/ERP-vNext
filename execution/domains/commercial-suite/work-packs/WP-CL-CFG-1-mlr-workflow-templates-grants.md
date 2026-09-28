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
