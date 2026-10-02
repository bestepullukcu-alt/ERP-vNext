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
