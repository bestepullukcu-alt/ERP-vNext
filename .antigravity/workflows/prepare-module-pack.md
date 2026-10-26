---
description: "PREPARE-MODULE-PACK — Kod yazmadan yeni modül sözleşmesi hazırlama workflow'u. Golden Reference (DEV-0000 Slim / DEV-0001 Compact) zorunlu şablon."
---

# /prepare-module-pack

Bu workflow yeni bir modülün geliştirmeye başlamadan önce module pack sözleşmesini hazırlar. Bu aşamada backend, frontend, gateway veya runtime kodu yazılmaz.

> **Tetikleme yolları:**
> - **Sıfırdan modül:** Kullanıcı yeni bir modül için doğrudan bu workflow'u çağırır.
> - **`bootstrap-domain` çıktısını rafine etme:** Excel'den toplu üretilen `draft` module pack'leri AC/iş kuralı/L10n anahtarı ile zenginleştirmek için bu workflow tekrar çağrılabilir; var olan pack içeriği `module-pack-author` tarafından **üzerine yazılmadan** detaylandırılır.

## 1. Giriş

Gerekli bilgiler:

- Modül adı
- Hedef domain veya domain adayları
- Servis (`Diten.Platform`, `Diten.MdmService`, `Diten.DevEnablementService`, `Diten.AuthService`)
- Shell tipi (`platform-admin` veya `tenant`)
- Kullanıcı create/edit form alanları (sayı + isimler)
- DataTable olup olmadığı
- Bilinen iş kuralları ve bağımlılıklar
- Entity base kararı (tenant-owned ise `EntityBase`/`BaseEntity`; cross-tenant Platform katalog ise `GlobalEntity` + gerekçe)

## 2. Zorunlu Bağlam Okuma (Sıra)

`module-pack-author` sırasıyla şunları okur — sapma kabul edilmez:

1. `AGENTS.md`
2. `execution/domains/{domain}/domain-config.md`
3. `execution/portfolio/master-development-plan.md` (modül envanteri, MVP scope, cross-cutting standartlar)
4. `execution/registries/module-id-registry.md` (canonical modül ID listesi)
5. `execution/delivery/platform-delivery-board.md` (blocker ve follow-up kontrolleri)
6. `.antigravity/rules/module-pack-standard.md`
7. **Golden Reference pack'i** (form alan sayısına göre):
   - Slim: `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`
   - Compact: `execution/domains/developer-enablement/module-packs/DEV-0001-golden-reference-compact.md`
8. **Gerçek Golden Reference kodu** (birebir şablon):
   - Backend: `services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/` (veya `Compact/`)
   - Frontend: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/` (veya `Compact/`)
9. `.antigravity/rules/views-organization.md`
10. `.antigravity/rules/handler-design.md`
11. `.antigravity/rules/erp-architecture.md`
12. `.antigravity/rules/response-envelope.md`
13. `.antigravity/rules/entity-base-template.md`
14. `.antigravity/rules/routes.md`

Platform admin shell modülü hazırlanıyorsa ek canlı referans: `frontend/Diten.Web/Views/Platform/Tenants/`.

## 3. Golden Reference Kararı

DataTable modüllerinde create/edit form alanları sayılır:

- `8 ve altı`: `golden_reference: slim`
- `8'den fazla`: `golden_reference: compact`

Sayılmayan alanlar: `Id`, `TenantId`, `IsDeleted`, `CreatedAt`, `UpdatedAt`, `DeletedAt`, audit alanları ve DataTable checkbox/action kolonları.

**Karar sonrası:** Golden Reference kodu (Slim veya Compact) **birebir şablon** olarak alınır. Folder yapısı, naming convention'ı, partial seti, handler hiyerarşisi sapmadan kopyalanır; sadece `{Module}` adı ve domain-spesifik alanlar değişir.

## 4. Shell ve Layout Kararı

| `shell` değeri | Razor Layout | View klasörü |
|---|---|---|
| `platform-admin` | `_LayoutPlatformAdmin` | `Views/Platform/{Module}/` |
| `tenant` | `_LayoutTenantShell` | `Views/{Area}/{Module}/` |
| `none` | — (backend-only) | — |

Pack'in **Layout & Shell Contract** bölümünde Razor `Layout = "..."` zorunluluğu açıkça yazılır ve acceptance criteria'da test edilebilir madde olarak yer alır.

## 5. Module Pack Üretimi

Yeni dosya `execution/domains/{domain}/module-packs/{ID}-{slug}.md` altında oluşturulur veya mevcut dosya güncellenir.

Varsayılan status:

```yaml
status: draft
```

Frontmatter (zorunlu alanlar):
```yaml
id, name, domain, service, shell, golden_reference, entity_base,
status, owner, branch, started, target, form_field_count
```

Pack gövdesi (21 zorunlu bölüm — `module-pack-standard.md` Bölüm 6):
1-8. Module Summary, Ownership, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints
9. Layout & Shell Contract
10. Backend File Convention
11. Frontend File Contract
12. Validation Rules
13. Failure Path to Verify
14. Authorization Convention
15. Gateway / API Routing Decision
16. Acceptance Criteria — yalnız yeni Control Tower'ların yeni paketlerinde her madde `AC-<n> (zorunlu|isteğe bağlı)` (`module-pack-standard.md` §7)
17. Test Expectations
18. Ready-for-dev Checklist
19. Implementation Notes
20. Follow-up Items
21. Audited Events — her yazma komutu → olay adı → yol (a/b/c) ya da istisna sınıfı + gerekçe (`module-pack-standard.md` Bölüm 10.1, AUD-001)
22. Record Codes — kullanıcıya görünen kodu olan her kayıt türü → sınıf (OTOMATİK / İKİSİ DE / KALSIN) · tür öneki · üretici (ortak kod hizmeti) · İKİSİ DE ise gerekçe kaydı (RCS-001, `.antigravity/rules/record-coding-standard.md`)

Kod üretimi için kullanıcı incelemesinden sonra status `approved` veya `ready-for-dev` yapılmalıdır.

## 6. Orchestrator Handoff

`@orchestrator` yalnızca mevcut ve onaylı module pack ile geliştirmeye başlar.

Module pack yoksa veya `draft` ise orchestrator kod yazmaz; kullanıcıyı bu workflow'a yönlendirir.

## 7. Ready-for-dev Geçişi Öncesi Kontrol

Pack `ready-for-dev`'e geçmeden önce **Ready-for-dev Checklist** bölümündeki tüm maddeler işaretli olmalı:

- [ ] Golden Reference (slim veya compact) referans olarak okundu
- [ ] Frontmatter tüm zorunlu alanlar dolu
- [ ] Layout & Shell Contract'ta Razor Layout açıkça yazılı
- [ ] Backend File Convention Golden Reference ile birebir
- [ ] Frontend File Contract Slim/Compact dosya listesi tam
- [ ] Validation Rules her field için yazılı
- [ ] Failure Path en az 4 senaryo
- [ ] Authorization Convention permission listesi + policy + actor type
- [ ] Gateway routing kararı açık
- [ ] Acceptance criteria test edilebilir maddeler
- [ ] Test expectations build/verifier/RESX/smoke kapsıyor
- [ ] Audited Events tablosu her yazma komutunu tam bir kez içeriyor; servisin denetim altyapısı var ya da `Dependencies`'te engel olarak yazılı (AUD-001)
- [ ] Record Codes tablosu kodlu her türü içeriyor; yeni önekler RCS-001 §3 tablosuna eklendi; modül kendi sayacını yazmıyor (RCS-001)
- [ ] Kapsam dışı bir modülde değişiklik gerekiyorsa kullanıcıya soruldu ve cevap `Dependencies` / `Follow-up Items`'ta yazılı (XMC-001)
- [ ] `Teslim dilimleri` tablosu var; paketin tamamını tek WP almıyor (CT-APR-02)
- [ ] Onaydan önce bağımsız inceleme yapıldı (Codex ya da Claude, ayrı oturum); bulgu dosyası ve `execution/approvals/reviews.tsv` satırı hazır, açık ENGEL yok (SOP §20.4)


---

## Module ID Canonicalization Gate (DCP-002)

The Blueprint (`docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx` :: `Blueprint_Data`) is the canonical authority for every `MOD-xxxx` ID and canonical name. Master 7 is historical predecessor evidence only. Before creating or reserving any `MOD-xxxx` (new module, FU/child, or reservation):

1. **Blueprint lookup** — the ID + canonical name must exist in `Blueprint_Data`, or the ID must be an FU/child of an existing Blueprint MOD parent.
2. **Registry collision** — it must not already map to a different capability in `execution/registries/module-id-registry.md`.
3. **Canonical-name validation** — the pack `name` must match the Blueprint canonical name (or an approved alias).
4. **Parent/FU/child decision** — decide explicitly whether the work is a new module or an FU/child of an existing module before minting an ID.
5. **No repo-only identity (Master-first)** — a capability absent from the Blueprint goes to a Module Change Request in the Master; no placeholder, next-free or repo-only ID may be invented. Enterprise Architect reservations recorded in the registry before 2026-10-16 stay as recorded (`--repo-only` proves them).
6. **Preflight (fail-closed)** — run `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-XXXX --name "Canonical Name" [--parent MOD-YYYY] [--repo-only]`. A non-zero exit BLOCKS pack creation.

Authority and policy: `execution/portfolio/delivery-capability-packs/DCP-002-module-identity-canonicalization.md`. Legacy (`PSS-*`, `NEW-*`) and repo-only IDs are valid only as deprecated aliases of a canonical MOD id; a missing canonical id is a Module Change Request in the Master (Master-first rule), not a repository reservation.

### Master-first rule and the closed CAND-CAP namespace (DCP-002, Blueprint 8.7)

**Master-first rule (CEO, 2026-10-16; Blueprint 8.7 `Module_Change_Requests`):** no module may be created, coded or registered outside the Master. When the Blueprint has no capability and no existing MOD/FU fits, **stop**: raise a Module Change Request in the Master's `Module_Change_Requests` sheet (through the Blueprint owner) and continue only after the Master gives a new MOD-ID or an approved mapping. Code, routes, permissions and data objects carry a MOD-ID that exists in `Blueprint_Data`.

`CAND-CAP-####` is a closed, historical namespace: no new candidate identity is opened. The existing ones are deprecated aliases of the canonical targets Master 8.2 / 8.7 gave them (`execution/portfolio/blueprint-master-plan-reconciliation.md` § Master 8.7 resolution). The candidate gate refuses a new or a deprecated candidate and names its canonical target:

`python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-#### --name "Capability Name"`

New-module identity rule: **Blueprint lookup → existing MOD or FU when available → otherwise a Module Change Request in the Master → never invent a MOD / PSS / NEW identity, and open no new CAND-CAP.**
