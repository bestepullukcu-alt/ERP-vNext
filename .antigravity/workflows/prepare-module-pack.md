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
3. `docs/platform/master-plan.md` (modül envanteri, MVP scope, cross-cutting standartlar)
4. `.antigravity/rules/module-pack-standard.md`
5. **Golden Reference pack'i** (form alan sayısına göre):
   - Slim: `execution/domains/developer-enablement/module-packs/DEV-0000-golden-reference-slim.md`
   - Compact: `execution/domains/developer-enablement/module-packs/DEV-0001-golden-reference-compact.md`
6. **Gerçek Golden Reference kodu** (birebir şablon):
   - Backend: `services/Diten.DevEnablementService/.../Features/GoldenReferenceSlim/` (veya `Compact/`)
   - Frontend: `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceSlim/` (veya `Compact/`)
7. `.antigravity/rules/views-organization.md`
8. `.antigravity/rules/handler-design.md`
9. `.antigravity/rules/erp-architecture.md`
10. `.antigravity/rules/response-envelope.md`
11. `.antigravity/rules/entity-base-template.md`
12. `.antigravity/rules/routes.md`

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

Pack gövdesi (20 zorunlu bölüm — `module-pack-standard.md` Bölüm 6):
1-8. Module Summary, Ownership, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Runtime Constraints
9. Layout & Shell Contract
10. Backend File Convention
11. Frontend File Contract
12. Validation Rules
13. Failure Path to Verify
14. Authorization Convention
15. Gateway / API Routing Decision
16. Acceptance Criteria
17. Test Expectations
18. Ready-for-dev Checklist
19. Implementation Notes
20. Follow-up Items

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
