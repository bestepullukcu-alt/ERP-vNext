# WORK PACKAGE — WP-KP-2-CFG · Bilgi Yolu MLR onay şablonları (canlı, 6 ülke) + yetki kontrolü — tenant 97c5 (tarayıcı / API, uygulama KODU YOK)

> **CT (SoR).** KP-2'nin (`ccf3d8fe`, birleşti) canlıda çalışması için gereken **konfigürasyon**. Desen: WP-CL-CFG-1 (iddialar; 7 şablon canlı, aynı pozisyonlar).
>
> **Ön koşul:** fleet restart yapılmış olmalı; KP-2 canlıda olmalı.
>
> **Veri yazma yetkisi** (kullanıcı bu paketi göndererek verir; yalnız bu liste):
> 1. 6 workflow şablonunun oluşturulması ve **yayınlanması** (tenant 97c5).
> 2. Yetki eksikse grant script'inin **yazılması** (dry-run varsayılan). Script'i **kullanıcı** `--apply` ile çalıştırır; **agent RBAC'a YAZMAZ.**
>
> **Çalışma yeri:** worktree `C:\tmp\kp-2-cfg`, dal `wp/kp-2-cfg`. Commit (rapor + varsa script) bu dala, push YOK. Canlı işlemler çalışan fleet'e (localhost:5001 / 5000).

## Kanıt (CT)
- **Pozisyonlar** (WP-ORG-02; CL-CFG-1'de doğrulandı):

  | Pozisyon | Id |
  |---|---|
  | POS-MED-DIR Medikal Direktör | `d0b6ce33-09ab-4789-bfaa-e49d53880fef` |
  | POS-MED-SPC Medikal Uzman | `5fd3988e-433a-4297-9085-eea42acef5b8` |
  | POS-LEG-CNS Hukuk Müşaviri | `7a72bd14-9112-4fd8-83d2-ca80d6ab9f81` |
  | POS-LEG-SPC Hukuk Uzmanı | `4967a03c-01ad-4909-8609-bc672e5fa51b` |
  | POS-REG-MGR Ruhsat Müdürü | `b122780a-1625-4a70-91ff-713fc2778111` |
  | POS-REG-SPC Ruhsat Uzmanı | `0f0b5c2c-c67f-42a7-a100-733e1fe784fc` |

  sema pullukcu: MED-DIR (Primary), LEG-CNS ve REG-MGR (Secondary).
- **Tanım biçimi ve API:** WP-CL-CFG-1 "Kanıt" bölümünün aynısı.
  - `POST api/v1/workflow/definitions` → `POST definitions/{id}/publish`;
  - ekran `/Platform/Workflow`, proxy `/Platform/Workflow/api/{**everything}`;
  - ayrıştırıcı `WorkflowDefinitionRuntimePlan.cs`.
- **Örnek:** canlıdaki `CLAIM-LOCAL-MLR-TR` tanımını oku ve kopyala; yalnız kod, ad ve nesne tipi değişir.
- **KP-2:**
  - `KnowledgePathReviewRules.ObjectType = "crm.knowledge-path-revision"`;
  - şablon kodu `KP-MLR-{CountryCode}` (ayar: `ConfigurationKnowledgePathReviewSettings`; varsayılanı oku, farklıysa raporla);
  - adım kodları CRM'de geçmiş okumasında kullanılıyorsa (adım adı) aynı tutulmalı.
- **Yetki anahtarları:** `crm.knowledge.path.read | manage | publish` (Web `KnowledgePathsController`, manifest `KNOWLEDGE_PATHS`).

## NE

### 1. 6 onay şablonu (canlı)
| TemplateCode | Ad |
|---|---|
| `KP-MLR-TR` | Bilgi Yolu — Türkiye MLR onayı |
| `KP-MLR-BY` | Bilgi Yolu — Belarus MLR onayı |
| `KP-MLR-UZ` | Bilgi Yolu — Özbekistan MLR onayı |
| `KP-MLR-TM` | Bilgi Yolu — Türkmenistan MLR onayı |
| `KP-MLR-GE` | Bilgi Yolu — Gürcistan MLR onayı |
| `KP-MLR-AZ` | Bilgi Yolu — Azerbaycan MLR onayı |

- **Her şablon:** tek aşama (`mlr`, "MLR İnceleme"), 3 sıralı adım. `CLAIM-LOCAL-MLR-*` ile aynı.

  | Adım | code | Adaylar | SLA |
  |---|---|---|---|
  | 1 Medikal inceleme | `medical` | POS-MED-DIR, POS-MED-SPC | 4320 dk; 2880'de POS-MED-DIR'e yükselt |
  | 2 Hukuk inceleme | `legal` | POS-LEG-CNS, POS-LEG-SPC | aynı → POS-LEG-CNS |
  | 3 Ruhsat inceleme | `regulatory` | POS-REG-MGR, POS-REG-SPC | aynı → POS-REG-MGR |

- `requestedObjectType`: **`crm.knowledge-path-revision`**.
- `commentRequired: false`. Ret yorumu zorunluluğu CRM karar ucunda (KP-2).
- Oluştur → **yayınla**.
- **Idempotent:** aynı kod varsa oluşturma. Yayınlı değilse raporla.

### 2. Yetki kontrolü (salt okuma) + gerekirse script
- 97c5 **Admin** rolünde `crm.knowledge.path.read / manage / publish` ve `platform.workflow.*` (instances / tasks) var mı kontrol et.
- **Eksik varsa:** `scripts/rbac/grant_knowledge_path_rbac_97c5.py`.
  - Desen: `grant_claims_v2_rbac_97c5.py`: dry-run varsayılan, BSON subtype-4 GUID, tarih alanları yerel bir 97c5 rolePermissions dokümanından aynen kopyalanır, idempotent, marker `manual-grant-knowledge-path-rbac`.
  - Kullanıcıya `--apply` talimatı verilir.
- **Eksik yoksa** script yazılmaz; rapor "yetki tam" der.

### 3. Duman testi (salt okuma ya da geri alınabilir)
- `GET api/v1/workflow/definitions?code=KP-MLR-TR` yayında görünür.
- Tanımda 3 adım ve doğru adaylar var.
- **Örnek başlatma YAPMA** (KP-UI-2 / E4'te CT yapar).

## KORU / YAPMA
- Kod değişikliği YOK. Mevcut `CLAIM-*` şablonlarına DOKUNMA.
- RBAC'a yazma YOK (yalnız script yazılır).
- Tarayıcıda kullanıcının oturum açık sekmesine harness / mock enjekte etme; ayrı sekme. Parola girme: kullanıcı giriş yapar.
- **DUR:**
  - KP-2'nin şablon kodu ayarı `KP-MLR-{CC}` değilse;
  - Platform `requestedObjectType`'ı serbest metin değil de kayıtlı bir listeden doğruluyorsa (`crm.knowledge-path-revision` kayıtlı değilse).
  - İki durumda da raporla.

## Acceptance
- **E4:**
  - 6 şablon yayında;
  - tanımlar doğru (3 sıralı adım, adaylar, SLA, nesne tipi);
  - yetki durumu raporlandı (script gerektiyse dry-run çıktısı);
  - `CLAIM-*` şablonları değişmedi.
- **Rapor:** şablon kodu → definition id + sürüm tablosu.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/integration-agent.md]
WP: WP-KP-2-CFG · Bilgi Yolu MLR onay şablonları (canlı, 6 ülke) + yetki kontrolü — tenant 97c5 (uygulama KODU YOK)
Repository: C:\tmp\kp-2-cfg (worktree) · Branch: wp/kp-2-cfg · commit (rapor + varsa script) bu dala, push YOK · Canlı: localhost:5001/5000 (fleet restart sonrası)

Amaç: KP-2'nin canlıda çalışması için 6 workflow şablonu KP-MLR-{TR,BY,UZ,TM,GE,AZ} (requestedObjectType crm.knowledge-path-revision; tek aşama mlr; 3 sıralı adım medical/legal/regulatory; adaylar MED/LEG/REG pozisyonları; SLA 4320/2880) oluştur + yayınla; 97c5 Admin'de crm.knowledge.path.* ve platform.workflow.* yetkilerini kontrol et, eksikse grant script'i YAZ (çalıştırma).

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-2-CFG-path-mlr-templates.md · …/WP-CL-CFG-1-mlr-workflow-templates-grants.md (desen + JSON biçimi + API) · …/WP-KP-2-path-revision-mlr.md (§37) · services/Diten.CrmService/src/**/Knowledge/Path/Review/KnowledgePathReviewCore.cs (ObjectType) + Infrastructure/**/ConfigurationKnowledgePathReviewSettings.cs (şablon kodu biçimi) · scripts/rbac/grant_claims_v2_rbac_97c5.py (script deseni) · memory agent-browser-harness-separate-tab, mongo-guid-subtype-write-recipe.

NE:
 1) 6 şablon (canlı, /Platform/Workflow ya da aynı-origin proxy): canlıdaki CLAIM-LOCAL-MLR-TR tanımını oku, kopyala; değişenler kod (KP-MLR-{CC}), ad ("Bilgi Yolu — {Ülke} MLR onayı"), requestedObjectType crm.knowledge-path-revision. commentRequired false (ret yorumu CRM'de). Oluştur → yayınla. Idempotent (varsa oluşturma; yayınsızsa raporla).
 2) Yetki: 97c5 Admin rolünde crm.knowledge.path.read/manage/publish + platform.workflow.* (instances/tasks) var mı (salt okuma). Eksikse scripts/rbac/grant_knowledge_path_rbac_97c5.py yaz (grant_claims_v2 deseni: dry-run varsayılan, subtype-4 GUID, tarih alanları yerel dokümandan, idempotent, marker manual-grant-knowledge-path-rbac) ve dry-run çıktısını rapora koy; RBAC'a YAZMA.
 3) Duman: GET definitions → 6 kod yayında, 3 adım + adaylar + nesne tipi doğru. Örnek başlatma YAPMA.
KORU/YAPMA: kod değişikliği YOK; CLAIM-* şablonlarına DOKUNMA; RBAC'a yazma YOK; kullanıcının oturumlu sekmesine harness/mock enjekte etme (ayrı sekme); parola girme (kullanıcı giriş yapar).
DOĞRULA (E4): 6 şablon yayında + tanımlar doğru + yetki durumu raporu + CLAIM-* değişmedi. Rapor: şablon kodu → definition id + sürüm tablosu. Commit ("chore(crm): WP-KP-2-CFG — knowledge path MLR workflow templates (6 countries) + rbac check" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: KP-2 şablon kodu ayarı KP-MLR-{CC} değilse ya da Platform requestedObjectType'ı kayıtlı listeden doğruluyor ve crm.knowledge-path-revision kayıtlı değilse DUR + raporla.
```

---

## §36.2 Uygulama raporu (owner, 2026-10-01) — canlı 97c5

**Ön koşul ve DUR kontrolü:**
- **Şablon kodu:** KP-2'de `KnowledgePathReviewDefaults.TemplateCodeFormat = "KP-MLR-{0}"`; `appsettings*.json` içinde `Crm:KnowledgePaths:Workflow` override'ı yok. Nesne tipi `crm.knowledge-path-revision`.
- **Nesne tipi doğrulaması:** Platform kodu `requestedObjectType`'ı hiçbir yerde okumuyor ya da doğrulamıyor; tanım JSON'unda serbest veri. Kayıtlı bir nesne tipi listesi yok.
- İki DUR koşulu da tetiklenmedi.
- **Oturum:** kullanıcı yerleşik tarayıcıda giriş yaptı; işlemler ayrı sekmede, aynı kaynaktaki `/Platform/Workflow/api/…` proxy'si üzerinden yapıldı. Sayfaya mock / harness eklenmedi, örnek başlatılmadı.
- **Başlangıç durumu:** `KP-MLR-*` yoktu. `CLAIM-*` (7) şablonlarının parmak izi alındı (şablon + sürüm dokümanlarının SHA-256'sı).

### 1. Şablonlar — 6/6 Published
- Kaynak: canlı `CLAIM-LOCAL-MLR-TR` v1 tanımı. Değişenler yalnız kod, ad ve `requestedObjectType`.
- İşlem: `POST definitions` (201) → `POST definitions/{id}/publish` (200), hepsi ilk denemede.

| Kod | Ad | Template id | Aktif sürüm (v1) | Durum |
|---|---|---|---|---|
| `KP-MLR-TR` | Bilgi Yolu — Türkiye MLR onayı | `b8ecd4c2-5384-4209-8e35-5b25304fd896` | `43e2d5d0-365d-459a-82a3-c6cb8318ed16` | Published, değişmez |
| `KP-MLR-BY` | Bilgi Yolu — Belarus MLR onayı | `4e194186-f220-4915-8204-562e667f181b` | `282fb5c9-f6a4-4562-8bd8-131a89dd98cf` | Published, değişmez |
| `KP-MLR-UZ` | Bilgi Yolu — Özbekistan MLR onayı | `4b7d4a23-824a-4c88-a538-50815240a60e` | `70067af0-ae2d-423a-9527-eb726f89a6c8` | Published, değişmez |
| `KP-MLR-TM` | Bilgi Yolu — Türkmenistan MLR onayı | `f27b9cf1-312b-435f-abe3-3fbd61547f8e` | `067edaeb-134e-444b-823f-0f23068b2d0e` | Published, değişmez |
| `KP-MLR-GE` | Bilgi Yolu — Gürcistan MLR onayı | `a9390574-51a9-4189-8750-8f54b0a217ac` | `9d552c94-5830-44e2-8960-9a2634606d87` | Published, değişmez |
| `KP-MLR-AZ` | Bilgi Yolu — Azerbaycan MLR onayı | `967ed5cc-cddc-44fc-a2ac-0fe2d8b839d5` | `01f220d5-a277-4093-a694-01b9d1022c5b` | Published, değişmez |

Yayınlayan: oturumdaki kullanıcı (`bestepullukcu@gmail.com`). `publishReason` = WP-KP-2-CFG.

### 2. Duman (salt okuma)
- `GET definitions`: 6 `KP-MLR-*` kodu `Published`. Her detayda `activePublishedVersionId` = yayınlı v1.
- Yayınlı JSON (6 şablonda aynı): `requestedObjectType: crm.knowledge-path-revision`, tek aşama `mlr` ("MLR İnceleme"), sıralı adımlar:
  - `medical` [MED-DIR, MED-SPC], 4320 / 2880 → MED-DIR;
  - `legal` [LEG-CNS, LEG-SPC], 4320 / 2880 → LEG-CNS;
  - `regulatory` [REG-MGR, REG-SPC], 4320 / 2880 → REG-MGR.
  - `commentRequired: false` (ret yorumu zorunluluğu CRM karar ucunda, KP-2).
- `requestedObjectType` dışında `CLAIM-LOCAL-MLR-TR` tanımıyla birebir aynı (JSON karşılaştırması).
- **`CLAIM-*` değişmedi:** 7 şablonun parmak izi önce ve sonra aynı.
- Örnek başlatılmadı (E4'te CT).

### 3. Yetki — tam, script gerekmedi
97c5 `Admin` rolünde (`6a315467-7d80-4ad8-bd76-78f8f762fe8a`) salt okuma kontrolü:
- `crm.knowledge.path.read / manage / publish`: **var**;
- `platform.workflow.instances.start / view`, `tasks.approve / reject / cancel / delegate / request-info`, `definitions.view / manage / publish`, `transitions.evaluate`, `escalations.*`: **var**.

Eksik yok; `grant_knowledge_path_rbac_97c5.py` yazılmadı, RBAC'a hiçbir şey yazılmadı.

### 4. Hata / sapma
Konsol hatası yok. Ağda 4xx / 5xx yok. Kod değişikliği yok.
