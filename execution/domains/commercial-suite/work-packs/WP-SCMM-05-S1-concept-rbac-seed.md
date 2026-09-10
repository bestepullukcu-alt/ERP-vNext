# WORK PACKAGE — WP-SCMM-05-S1 · Canonical Knowledge/Concept RBAC key seed (SCMM-side)

> **Control Tower kaydı (SoR).** SCMM-05 gate'inin **SCMM-side (bizim) remediation'ı S1.** Owning-team'e bağlı DEĞİL. **Açar:** SCMM-09 (concept extend) authz'ı.
> **Bağlı:** SCMM-04 ✅ contract framework, SCMM-05 gate (WP-SCMM-05).

## Metadata
```text
WP ID:            WP-SCMM-05-S1
Prompt ID:        P-SCMM-S1 · v1.0
Task Class:       Backend RBAC seed + controller switch (state-changing config)
Golden-Flow Profile: B
Risk Class:       HIGH (§9.1 — authorization + seed)
Capability:       SCMM · MOD-0162 (concept foundation) + CAND-CAP-0011
Agent Lane:       AL-SCMM-RBAC (DEV)
Target Agent:     backend-architect  (RBAC deseni security-agent kuralına sadık)
Branch:           feature/structured-content-messaging · Expected HEAD: 9187b566
Evidence hedefi:  E2 (build + seed-catalog testi + controller canonical-key kullanımı) + E3 (live 200 grant / 403 no-grant) fleet açılınca
```

## Ölçülmüş gerçek (CT, 2026-09-07)
- Canonical keyler **tanımlı ama seed edilmemiş**: `ConceptPermissions.cs` → `crm.knowledge.concept.read/manage`, `concept-template.manage`, `concept-link.manage`; DEV-ONLY fallback = `crm.territory.read`/`crm.territory.model.manage` (`ConceptPermissions.cs:19-21`, "prod tenant'a verilmemeli").
- Seed yolu = **AuthService DataSeeder.cs**: katalog deseni `new("crm","<resource>","<action>","<name>","<desc>", moduleOverride:"crm-<module>")` (satır ~216+); role-grant = key-array + grant metodu (territoryKeys deseni ~1013). `crm.knowledge` seed'de **0**.
- ⚠️ **Fallback 218 yerde** kullanılıyor (Campaigns/Consent/Concept/Content/Path…) — **SCMM ötesi yaygın CRM borcu.** **S1 yalnız SCMM concept-foundation yüzeyini** kapsar; Campaign(MOD-0165)/Consent(MOD-0164) controller'larına DOKUNMAZ.

## Kapsam (SCMM concept-foundation yüzeyi — SADECE)
SCMM'in tükettiği Knowledge permission sınıfları + controller'ları: **ConceptTypes, ConceptChainTemplates, ContentConceptLinks, KnowledgeContents, KnowledgePaths, KnowledgeSubjects, KnowledgeAudienceProfiles**. Bunların canonical key'lerini seed et + grant + controller'ları fallback'ten canonical'a çevir. **Campaign/Consent/Territory/Account/Contact/Visit vb. dokunulmaz.**

## Üç edit
1. **DataSeeder katalog** — her SCMM knowledge canonical key için `new("crm","knowledge.concept",...)` vb. satır (mevcut `new("crm",...)` desenine birebir, moduleOverride:"crm-knowledge").
2. **DataSeeder role-grant** — bu key'leri, bugün territory-fallback'i taşıyan **aynı CRM rol(ler)ine** ver (erişim regresyonu olmasın); territoryKeys grant deseniyle.
3. **CrmService controller switch** — SCMM knowledge controller'larında `[HasPermission(Perms.ReadFallback/ManageFallback)]` → canonical (`Perms.Read/Manage/TemplateManage/LinkManage`). Fallback sabitleri sınıfta kalabilir (başka modül hâlâ kullanıyor) ama SCMM controller'ları artık canonical kullanır.

## Acceptance
- E2: build temiz; test → canonical key'ler seed kataloğunda (dosya:satır); SCMM knowledge controller'ları artık fallback DEĞİL canonical key ref ediyor (grep kanıtı).
- E3 (fleet açılınca): canonical key'i olan rolle 200; olmayan tenant kullanıcısıyla 403. Fleet cold ise §26 operator kanıtı, uydurma yok (K10).
- Erişim regresyonu YOK: fallback'i taşıyan roller artık canonical'ı da taşır.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-05-S1 · Prompt P-SCMM-S1 v1.0  (RBAC seed + controller switch — SCMM concept-foundation SADECE)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 9187b566 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-05-authz-audit-readiness-gate.md (§37 S1 tanımı)
2. services/Diten.CrmService/.../Features/Knowledge/Concept/ConceptPermissions.cs (canonical + fallback keyler)
3. services/Diten.AuthService/.../Seed/DataSeeder.cs (katalog deseni ~216; crm.territory grant ~1013)

BAĞLAM (ölçüldü): SCMM knowledge canonical keyler tanımlı ama SEED EDİLMEMİŞ; SCMM controller'ları DEV-ONLY
  crm.territory.* fallback'iyle çalışıyor ("prod'a verilmemeli"). Fallback 218 yerde — AMA S1 YALNIZ SCMM
  concept-foundation yüzeyini kapsar (Campaign/Consent/Account/Contact/Territory DOKUNULMAZ).

Kapsam (SADECE bu Knowledge yüzeyi): ConceptTypes, ConceptChainTemplates, ContentConceptLinks, KnowledgeContents,
  KnowledgePaths, KnowledgeSubjects, KnowledgeAudienceProfiles. Bunların permission sınıflarını (Concept*Permissions
  ve varsa sibling Knowledge*Permissions) enumerate et.

NE:  (1) DataSeeder KATALOG: her SCMM knowledge canonical key için new("crm","<resource>","<action>",<name>,<desc>,
         moduleOverride:"crm-knowledge") — mevcut new("crm",...) desenine BİREBİR.
     (2) DataSeeder ROLE-GRANT: bu key'leri, bugün crm.territory fallback'i taşıyan AYNI CRM rol(ler)ine grant et
         (territoryKeys grant desenine sadık) — erişim regresyonu OLMASIN.
     (3) CrmService SCMM knowledge controller'larında [HasPermission(Perms.ReadFallback/ManageFallback)] →
         canonical (Perms.Read/Manage/TemplateManage/LinkManage). Fallback sabitleri sınıfta KALABİLİR.
NASIL: mevcut DataSeeder + ConceptPermissions desenini birebir izle; yeni RBAC modeli kurma; GUID/subtype ile oynama.
       PKS-001 (lowercase-dotted, ≥3 segment) korunur.
YAPMA: SCMM DIŞI controller/permission (Campaign/Consent/Account/Contact/Territory/Visit) DEĞİŞTİRME. Global serializer/
       authz modeli değiştirme. HR&TEP/PVG'ye dokunma. MOD-0018/0021 kodunu hardenleme (o R1/R2 owning-team).
DOĞRULA (E2 + E3):
     - build temiz; test: canonical keyler seed kataloğunda + SCMM controller'ları canonical ref ediyor (grep: SCMM
       knowledge controller'larında ReadFallback/ManageFallback kalmadı).
     - E3 (fleet açılınca): canonical-key'li rol → 200; key'siz tenant kullanıcı → 403. Fleet cold → §26 operator kanıtı, UYDURMA.
     - erişim regresyonu yok (fallback rolleri artık canonical taşır).
Ayrı commit'ler: (a) seed catalog+grant, (b) controller switch. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: canonical key sınıfı bulunamazsa · seed grant hedef rolü belirsizse · kapsam SCMM dışına taşarsa. DUR + raporla.
```

---

## §37 CT bağımsız doğrulama (2026-09-07) → **ACCEPTED (E2)**
```text
Commits: 4231dd9d (seed catalog+grant) · cfa44307 (controller switch)
Agent: PASS · Verification: PASS (CT path:line + build/test teyit) · CT: ACCEPTED · Evidence: E2 (E3 fleet-cold)
```
- ✅ Kapsam: (a) DataSeeder.cs + KnowledgePermissionSeedTests.cs; (b) **13 Knowledge*Controller** — SCMM-dışı controller YOK (grep uyarısı commit-mesajına takıldı, dosya listesi temiz).
- ✅ **Escalation temiz:** `SeedTenant97c5CrmKnowledgeGrantAsync` → yalnız tenant-97c5 Admin (territory grant'i ile aynı rol), broadened DEĞİL.
- ✅ 11 canonical key seed'de (`new("crm","knowledge...")`, moduleOverride `crm-knowledge`); concept×4 + knowledge×2 + subject×2 + path×3.
- ✅ In-scope controller'larda fallback **0**; Journey (kapsam dışı) 12 korundu.
- ✅ CT kendi koşumu: **AuthService KnowledgePermissionSeedTests 28/28**, **CrmService.Api build 0 Hata**.
- ⏳ **E3 (live 200/403) NOT MEASURED — fleet cold**; fleet açılınca §26 operator turu.

**Sonuç:** S1 authz'ı **SCMM-09 için açtı** (canonical keyler var + granted + SCMM controller'ları kullanıyor). Residual: F-RBAC (controller XML doc prose hâlâ "dev fallback" der — kozmetik) · FU05 Journey canonical (ayrı) · E3 runtime.
