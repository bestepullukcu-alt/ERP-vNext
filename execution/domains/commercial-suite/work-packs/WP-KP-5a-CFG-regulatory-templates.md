# WORK PACKAGE — WP-KP-5a-CFG · Regülasyon onay şablonları (canlı, 6 ülke) + güvenlilik metni / yasal profil yetkileri — tenant 97c5 (tarayıcı / API, uygulama KODU YOK)

> **CT (SoR), 2026-10-02.** KP-5a (`3887370d`) + KP-5a-UI (`bfaa1902`) birleşti; canlıda çalışmaları için gereken **konfigürasyon**. Desen: WP-KP-2-CFG (6 `KP-MLR-*` şablonu canlı, §36.2).
>
> **Ön koşul (kullanıcı):** `test/crm-content-visit-e2e` (`949b6144` ve sonrası) ile **fleet restart** — Auth yeni 6 izni kataloğa ekler (DataSeeder), CRM `safety-texts` / `country-legal-profiles` uçları, Platform manifest iki sayfa, Web ekranlar canlıya gelir. Kullanıcı yerleşik tarayıcıda giriş yapmış olmalı.
>
> **Veri yazma yetkisi** (kullanıcı bu paketi göndererek verir; yalnız bu liste):
> 1. 6 workflow şablonunun oluşturulması ve **yayınlanması** (tenant 97c5).
> 2. Grant script'inin **yazılması** (dry-run varsayılan). Script'i **kullanıcı** `--apply` ile çalıştırır; **agent RBAC'a YAZMAZ.**
>
> **Çalışma yeri:** worktree `C:\tmp\kp-5a-cfg`, dal `wp/kp-5a-cfg`. Commit (rapor + script) bu dala, push YOK. Canlı işlemler çalışan fleet'e (localhost:5001 / 5000).

## Kanıt (CT)
- **KP-5a:** şablon kodu `RegulatoryTextReviewDefaults.TemplateCodeFormat = "KP-REG-{0}"` (`Features/Knowledge/Regulatory/RegulatoryTextCore.cs:46`; ayar `ConfigurationRegulatoryTextReviewSettings` — `appsettings*`'te override var mı oku). ObjectType'lar `crm.safety-text`, `crm.country-legal-profile`; **iki tür aynı ülke şablonunu kullanır**.
- **Pozisyonlar** (WP-KP-2-CFG): POS-REG-MGR `b122780a-1625-4a70-91ff-713fc2778111`, POS-REG-SPC `0f0b5c2c-c67f-42a7-a100-733e1fe784fc`. sema pullukcu: REG-MGR (Secondary).
- **Örnek tanım:** canlı `KP-MLR-TR` v1'in **`regulatory` adımı** (adaylar, SLA 4320 / 2880 → REG-MGR). Platform `requestedObjectType`'ı doğrulamıyor (KP-2-CFG §36.2).
- **API / ekran:** `POST api/v1/workflow/definitions` → `POST definitions/{id}/publish`; ekran `/Platform/Workflow`, proxy `/Platform/Workflow/api/{**everything}`.
- **Yeni izinler (KP-5a, Auth kataloğu; hiçbir role verilmedi):** `crm.safety-text.read | manage | submit`, `crm.country-legal-profile.read | manage | submit`. Web sayfaları (`/CRM/SafetyTexts`, `/CRM/LegalProfiles`) ve manifest (`SAFETY_TEXTS` 150, `LEGAL_PROFILES` 160) bu anahtarlarla açılır; karar Platform görev izinleriyle.
- **Kullanıcı (2026-10-02):** yalnız Regülasyon onaylar (tam MLR değil); ülke yasal profili de aynı akış (CT varsayılanı). Auth izin açıklamaları İngilizce kalır (7 dil yok — dokunma).

## NE

### 1. 6 Regülasyon şablonu (canlı)
| TemplateCode | Ad |
|---|---|
| `KP-REG-TR` | Güvenlilik / Yasal metin — Türkiye Regülasyon onayı |
| `KP-REG-BY` | Güvenlilik / Yasal metin — Belarus Regülasyon onayı |
| `KP-REG-UZ` | Güvenlilik / Yasal metin — Özbekistan Regülasyon onayı |
| `KP-REG-TM` | Güvenlilik / Yasal metin — Türkmenistan Regülasyon onayı |
| `KP-REG-GE` | Güvenlilik / Yasal metin — Gürcistan Regülasyon onayı |
| `KP-REG-AZ` | Güvenlilik / Yasal metin — Azerbaycan Regülasyon onayı |

- **Her şablon:** tek aşama `regulatory` ("Ruhsat İnceleme"), **tek adım**:

  | Adım | code | Adaylar | SLA |
  |---|---|---|---|
  | 1 Ruhsat inceleme | `regulatory` | POS-REG-MGR, POS-REG-SPC | 4320 dk; 2880'de POS-REG-MGR'e yükselt |

- Adım yapısı `KP-MLR-TR`'nin `regulatory` adımından birebir; yalnız kod, ad, aşama / adım sayısı ve nesne tipi değişir.
- `requestedObjectType`: **`crm.safety-text`** (bilgi amaçlı; Platform doğrulamıyor; iki tür aynı şablonu kullanır — raporda belirt).
- `commentRequired: false` (ret yorumu CRM karar ucunda, KP-5a).
- Oluştur → **yayınla**. **Idempotent:** aynı kod varsa oluşturma; yayınlı değilse raporla.

### 2. Yetki kontrolü (salt okuma) + grant script'i
- 97c5'te **Admin** rolü ve **sema**'nın rol(ler)i: 6 yeni anahtar + `platform.workflow.instances.start / view`, `platform.workflow.tasks.approve / reject` var mı.
- Yeni 6 anahtar hiçbir rolde değil (beklenen) → **`scripts/rbac/grant_regulatory_text_rbac_97c5.py`**:
  - **Admin:** 6 anahtarın tamamı;
  - **sema'nın rolü Admin değilse:** o role yalnız `crm.safety-text.read` + `crm.country-legal-profile.read` (karar için ayrıntı sayfası; karar yetkisi Platform görevinde);
  - desen `grant_claims_v2_rbac_97c5.py` / `grant_knowledge_path_rbac_97c5.py`: **dry-run varsayılan**, BSON **subtype-4 GUID** (memory `mongo-guid-subtype-write-recipe`, `rolepermission-guid-subtype-login-500`), tarih alanları yerel bir 97c5 rolePermissions dokümanından aynen kopyalanır, idempotent, marker `manual-grant-regulatory-text-rbac`.
  - Önce Auth kataloğunda 6 anahtarın **var olduğunu** doğrula (fleet restart sonrası seed). Yoksa DUR.
- Kullanıcıya `--apply` + **yeniden giriş** talimatı.

### 3. Duman testi (salt okuma)
- `GET api/v1/workflow/definitions` → 6 `KP-REG-*` `Published`, `activePublishedVersionId` dolu; tanımda tek adım + doğru adaylar.
- `KP-MLR-*` ve `CLAIM-*` şablonlarının parmak izi önce / sonra aynı.
- Kullanıcı script'i çalıştırıp yeniden girdikten sonra (rapor ikinci turda): menüde "Safety Texts" ve "Legal Profiles" görünür; iki liste boş ama hatasız açılır (konsol / ağ 4xx-5xx yok).
- **Örnek başlatma YAPMA, kayıt oluşturma YAPMA** (E4'te CT).

## KORU / YAPMA
- Kod değişikliği YOK. `CLAIM-*` ve `KP-MLR-*` şablonlarına DOKUNMA.
- RBAC'a yazma YOK (yalnız script). Auth izin açıklamalarına DOKUNMA (İngilizce kalır).
- Tarayıcıda kullanıcının oturum açık sekmesine harness / mock enjekte etme; **ayrı sekme**. Parola girme: kullanıcı giriş yapar.
- **DUR:**
  - KP-5a şablon kodu ayarı `KP-REG-{CC}` değilse (override);
  - 6 yeni izin Auth kataloğunda yoksa (fleet eski dalla çalışıyor olabilir);
  - Platform `requestedObjectType`'ı kayıtlı bir listeden doğruluyorsa.

## Acceptance
- **E4:** 6 şablon yayında; tanımlar doğru (tek adım, adaylar, SLA); grant script dry-run çıktısı raporda; `CLAIM-*` / `KP-MLR-*` değişmedi; (kullanıcı `--apply` + yeniden giriş sonrası) iki menü sayfası açılıyor.
- **Rapor:** şablon kodu → definition id + aktif sürüm tablosu; rol → eklenecek anahtarlar tablosu.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/integration-agent.md]
WP: WP-KP-5a-CFG · Regülasyon onay şablonları (canlı, 6 ülke) + güvenlilik metni / yasal profil yetkileri — tenant 97c5 (uygulama KODU YOK)
Repository: C:\tmp\kp-5a-cfg (worktree) · Branch: wp/kp-5a-cfg · commit (rapor + script) bu dala, push YOK · Canlı: localhost:5001/5000 (fleet restart sonrası)

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-KP-5a-CFG-regulatory-templates.md — önce tamamını oku. Ayrıca: …/WP-KP-2-CFG-path-mlr-templates.md (§36.2 rapor: canlı KP-MLR-* tanımları, pozisyonlar) · …/WP-KP-5a-safety-text-legal-profile.md (§37) · services/Diten.CrmService/src/Diten.CrmService.Application/Features/Knowledge/Regulatory/RegulatoryTextCore.cs · Infrastructure/Workflow/ConfigurationRegulatoryTextReviewSettings.cs + CrmService.Api appsettings*.json · scripts/rbac/grant_claims_v2_rbac_97c5.py ve grant_knowledge_path_rbac_97c5.py (varsa; desen) · memory agent-browser-harness-separate-tab, mongo-guid-subtype-write-recipe, rolepermission-guid-subtype-login-500, workflow-position-assignment.

NE: (1) 6 şablon KP-REG-{TR,BY,UZ,TM,GE,AZ}: tek aşama regulatory ("Ruhsat İnceleme"), tek adım regulatory [POS-REG-MGR, POS-REG-SPC], SLA 4320 / 2880 → REG-MGR — canlı KP-MLR-TR v1'in regulatory adımından birebir; requestedObjectType crm.safety-text (bilgi amaçlı, iki tür aynı şablon); commentRequired false; oluştur → yayınla; idempotent. (2) Salt okuma yetki kontrolü: 97c5 Admin + sema'nın rol(ler)i — 6 yeni anahtar (crm.safety-text.{read,manage,submit}, crm.country-legal-profile.{read,manage,submit}) + platform.workflow instances/tasks; önce 6 anahtarın Auth kataloğunda var olduğunu doğrula. scripts/rbac/grant_regulatory_text_rbac_97c5.py YAZ (çalıştırma): Admin'e 6 anahtar; sema'nın rolü Admin değilse o role yalnız iki .read; dry-run varsayılan, subtype-4 GUID, tarih alanları yerel 97c5 rolePermissions dokümanından kopya, idempotent, marker manual-grant-regulatory-text-rbac; dry-run çıktısını rapora koy; kullanıcıya --apply + yeniden giriş talimatı. (3) Duman (salt okuma): 6 KP-REG-* Published + doğru tanım; KP-MLR-* ve CLAIM-* parmak izi önce/sonra aynı; örnek başlatma ve kayıt oluşturma YOK.
KORU/YAPMA: kod değişikliği YOK; CLAIM-* / KP-MLR-* DOKUNMA; RBAC'a yazma YOK; Auth izin açıklamalarına DOKUNMA; kullanıcının oturum sekmesine harness/mock enjekte etme, ayrı sekme; parola girme.
DOĞRULA (E4): yukarıdaki duman + rapor tabloları (şablon kodu → definition id + aktif sürüm; rol → eklenecek anahtarlar). Commit ("chore(crm): WP-KP-5a-CFG — regulatory approval templates (6 countries) + regulatory text rbac grant script" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: şablon kodu ayarı KP-REG-{CC} değilse; 6 izin Auth kataloğunda yoksa; Platform requestedObjectType'ı kayıtlı listeden doğruluyorsa → DUR + raporla.
```
---

## §36.2 Uygulama raporu (owner, 2026-10-02) — canlı 97c5

**Ön koşul ve DUR kontrolü:**
- **Şablon kodu:** `RegulatoryTextReviewDefaults.TemplateCodeFormat = "KP-REG-{0}"`; CrmService.Api `appsettings*.json` içinde `Crm:RegulatoryTexts:Workflow` override'ı yok.
- **Auth kataloğu:** 6 yeni anahtar mevcut (Scope 0 = Tenant, Module `crm-knowledge`) — fleet doğru dalla çalışıyor.
- **Nesne tipi doğrulaması:** Platform kodunda `requestedObjectType` hiç geçmiyor; kayıtlı bir nesne tipi listesi yok (serbest veri).
- Üç DUR koşulu da tetiklenmedi.
- **Oturum:** kullanıcı yerleşik tarayıcıda giriş yaptı; işlemler ayrı sekmede, aynı kaynaktaki `/Platform/Workflow/api/…` proxy'si üzerinden yapıldı. Mock / harness eklenmedi, örnek başlatılmadı, CRM kaydı oluşturulmadı.
- **Başlangıç durumu:** `KP-REG-*` yoktu. `CLAIM-*` (7) ve `KP-MLR-*` (6) şablonlarının parmak izi alındı (şablon + sürüm dokümanlarının SHA-256'sı, Mongo `diten_personalization_dev`).

### 1. Şablonlar — 6/6 Published
- Kaynak: canlı `KP-MLR-TR` v1'in `regulatory` adımı, birebir. Değişenler: kod, ad, tek aşama / tek adım, `requestedObjectType`.
- İşlem: `POST definitions` (201) → `POST definitions/{id}/publish` (200), hepsi ilk denemede. `publishReason` = WP-KP-5a-CFG.

| Kod | Ad | Template id | Aktif sürüm (v1) | Durum |
|---|---|---|---|---|
| `KP-REG-TR` | Güvenlilik / Yasal metin — Türkiye Regülasyon onayı | `98a42638-ac41-4aab-b897-6239fce48a3f` | `ba00ebe5-950e-4c1a-a1c8-60b513686782` | Published |
| `KP-REG-BY` | Güvenlilik / Yasal metin — Belarus Regülasyon onayı | `f8c1ae04-e78b-4bf5-9e95-d0108ec3a9ac` | `52401c7b-1025-4523-b315-8a2e345d46c9` | Published |
| `KP-REG-UZ` | Güvenlilik / Yasal metin — Özbekistan Regülasyon onayı | `4c54aea9-6ca6-4b50-b4e9-a78f367e505b` | `f3f43b23-ea53-495d-80ef-51848887621a` | Published |
| `KP-REG-TM` | Güvenlilik / Yasal metin — Türkmenistan Regülasyon onayı | `6b2d751b-4e1f-4394-a387-29b7fb896b6a` | `06ab42cd-cdaf-4f00-8bf2-c179194f13cd` | Published |
| `KP-REG-GE` | Güvenlilik / Yasal metin — Gürcistan Regülasyon onayı | `795a0ca7-3d03-445c-8fc4-c80223138de7` | `ff4a4ba3-3b37-4da3-9163-b9a352487c6c` | Published |
| `KP-REG-AZ` | Güvenlilik / Yasal metin — Azerbaycan Regülasyon onayı | `bd905eda-d04a-4345-9a9c-3f66144379f7` | `569804b4-4424-41c0-8609-24b7f2062df3` | Published |

### 2. Duman (salt okuma)
- `GET definitions/{id}` + yayınlı sürüm: 6 şablonda `Published`, `activePublishedVersionId` = yayınlı v1.
- Yayınlı JSON (6 şablonda aynı): `requestedObjectType: crm.safety-text`, tek aşama `regulatory` ("Ruhsat İnceleme"), tek adım `regulatory` ("Ruhsat inceleme") [POS-REG-MGR `b122780a…`, POS-REG-SPC `0f0b5c2c…`], SLA 4320 / 2880 → POS-REG-MGR, `commentRequired: false` (ret yorumu CRM karar ucunda, KP-5a).
- **`requestedObjectType` bilgi amaçlı:** CRM iki türü (`crm.safety-text`, `crm.country-legal-profile`) aynı ülke şablonuyla başlatır; Platform alanı doğrulamaz. Yasal profil örneği de bu şablonla başlar, örneğin kendi `ObjectType`'ı CRM'in gönderdiği değerdir.
- **`CLAIM-*` ve `KP-MLR-*` değişmedi:** 13 şablonun parmak izi önce ve sonra aynı.
- Konsol hatası yok; ağda 4xx / 5xx yok.

### 3. Yetki — script yazıldı, RBAC'a yazılmadı
97c5 salt okuma kontrolü:

| Rol | 6 yeni anahtar | `platform.workflow.instances.start / view`, `tasks.approve / reject` |
|---|---|---|
| `Admin` (`6a315467-7d80-4ad8-bd76-78f8f762fe8a`) | yok | var |
| `GQD` (`7832826f-…`) | yok | var |
| `QADocumentation` (`e48a918e-…`) | yok | var |

- sema (`b.pullukcu@grandmedical.eu`, `d27fa4a6-…`) rolleri: **Admin**, GQD, QADocumentation → Admin olduğu için ayrı okuma rolü gerekmiyor.
- Not: platform kiracısındaki (`…0001`) `SuperAdmin` / `Viewer` rolleri yeni anahtarları seed'den almış (sistem deseni); 97c5'i etkilemez.

**Rol → eklenecek anahtarlar:**

| Rol | Eklenecek |
|---|---|
| `Admin` | `crm.safety-text.read / manage / submit`, `crm.country-legal-profile.read / manage / submit` |
| (Admin olmayan karar verici, `--reader-role` ile) | yalnız `crm.safety-text.read`, `crm.country-legal-profile.read` — 97c5'te bugün gerekmiyor |

**Script:** `scripts/rbac/grant_regulatory_text_rbac_97c5.py` — dry-run varsayılan, subtype-4 GUID, tarih alanları yerel 97c5 rolePermissions dokümanından kopya, idempotent, marker `manual-grant-regulatory-text-rbac`, rol / izin oluşturmaz. Dry-run çıktısı:

```
role 'Admin' (6a315467-7d80-4ad8-bd76-78f8f762fe8a): 6 grant(s) to insert: ['crm.safety-text.read', 'crm.safety-text.manage', 'crm.safety-text.submit', 'crm.country-legal-profile.read', 'crm.country-legal-profile.manage', 'crm.country-legal-profile.submit']
DRY-RUN — nothing written. Re-run with --apply.
```

**Kullanıcı adımı:** `py -3 scripts/rbac/grant_regulatory_text_rbac_97c5.py --apply` → Admin rolündeki kullanıcılar **yeniden giriş** yapar. Geri alma: `db.rolePermissions.deleteMany({CreatedBy: "manual-grant-regulatory-text-rbac"})`.

### 4. Kalan (ikinci tur, kullanıcı `--apply` + yeniden giriş sonrası)
- Menüde "Safety Texts" ve "Legal Profiles" görünür; iki liste boş ama hatasız açılır (konsol / ağ 4xx-5xx yok).
- E4 (CT): TR / tr güvenlilik metni → gönder → `KP-REG-TR` örneği → sema onayı → aktif → çözümleme.

### 5. Hata / sapma
Yok. Kod değişikliği yok.
### 6. İkinci tur (kullanıcı `--apply` + yeniden giriş sonrası, 2026-10-02)
- **Script uygulandı (kullanıcı):** marker `manual-grant-regulatory-text-rbac` ile 6 `rolePermissions` satırı, tüm GUID'ler subtype-4; dry-run tekrarında "0 grant(s) to insert" (idempotent).
- **Menü:** "Güvenlilik Metinleri" (`/CRM/SafetyTexts`) ve "Ülke Yasal Profilleri" (`/CRM/LegalProfiles`) görünür; iki sayfa 403'süz açılır, "Yeni" düğmesi görünür (manage yetkisi).
- **Listeler:** ikisi boş ve hatasız ("Tabloda veri bulunmuyor", 0 kayıt). Liste, ülke ekseni (7 seçenek) ve MDM ürün seçicisi (178 seçenek) istekleri 200.
- **Ara bulgu:** ilk kontrolde CRM servisi (5061) çalışmıyordu (süreç yok, `/health` yanıtsız) → tüm `/CRM/*/api` istekleri gövdesiz 502 (Bilgi Yolları dahil). Kullanıcı CRM'i başlattıktan sonra hepsi 200. Konsoldaki 502 kayıtları yalnız o döneme ait.
- **Küçük gözlem (hata değil):** liste yüklenirken iki istek gidiyor (`draw=1`, `draw=2`) — fabrikanın kayıtlı görünümü uygulaması; davranış diğer sunucu modu listeleriyle aynı.
- Kayıt oluşturulmadı, örnek başlatılmadı. **E4 (CT):** TR / tr güvenlilik metni → gönder → `KP-REG-TR` → sema onayı → aktif → çözümleme.
