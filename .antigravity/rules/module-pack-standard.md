# Module Pack Standard (ERP-vNext)

Bu standart, `execution/domains/{domain}/module-packs/{DOMAIN}-{NNN}-{slug}.md` dosyalarinin minimum formatini tanimlar.

## 1. File Naming

Zorunlu format:

```text
{DOMAIN}-{NNN}-{slug}.md
```

Kurallar:
- `DOMAIN`: `MDM` | `PSS` | `ESBP`
- `NNN`: 3 haneli sira numarasi (`001`, `002`, ...)
- `slug`: kucuk harf + tire ayirici (`product-management`)

Ornekler:
- `MDM-001-product-management.md`
- `PSS-002-role-permission-matrix.md`
- `ESBP-001-strategy-core.md`

## 2. YAML Frontmatter (Required)

Her module pack dosyasi asagidaki frontmatter ile baslamalidir:

```yaml
---
id: MDM-001
name: Product Management
domain: master-data-management
status: draft
owner: ali.tufanoglu
branch: feature/mdm/mdm-001-product-management
started: 2026-04-20
target: 2026-05-05
---
```

Alan kurallari:
- `id`: `{DOMAIN}-{NNN}`
- `name`: insan-okunur modul adi
- `domain`: domain folder adi ile birebir ayni olmali
- `status`: `draft | in-progress | review | done | blocked`
- `owner`: sorumlu kisi veya ekip
- `branch`: `feature/{domain-short}/{id-lower}-{slug}`
- `started` / `target`: `YYYY-MM-DD`

## 3. Required Sections

Frontmatter altinda asagidaki bolumler zorunludur:

1. `Module Summary`
2. `Ownership and Boundaries`
3. `Repo Scope`
4. `Protected Paths`
5. `Dependencies`
6. `Runtime Constraints`
7. `Acceptance Criteria`
8. `Test Expectations`
9. `Implementation Notes`
10. `Follow-up Items`

## 4. Repo Scope Rules

`Repo Scope` bolumu:
- Somut klasor/dosya yollari icermeli
- Dokunulacak yerleri net listelemeli
- Gerekirse API gateway dosyasini module-level kisitla belirtmeli

`Protected Paths` bolumu:
- Dokunulmayacak alanlari acik yazmali
- En az `.antigravity/**` ve domain-disi servis yollarini icermeli

## 5. Acceptance Criteria Rules

Acceptance criteria maddeleri test edilebilir olmali:
- Belirsiz ifadeler (`iyilestirildi`, `duzgun calisiyor`) kullanilmaz
- Somut endpoint, UI davranisi, localization ve quality gate adimlari yazilir
- DataTable modulunde `verify_datatable_page.py` ve `quality-gate-datatable` atiflari zorunludur

## 6. Test Expectations Rules

Minimum beklenti:
- Tenant isolation kontrolu
- Soft delete davranisi
- Browser smoke test sonucu

Module ozelligine gore unit/integration test kapsamı acik yazilmalidir.

## 7. Lifecycle Rules

Status akisi:

```text
draft -> in-progress -> review -> done
```

Alternatif durum:
- `blocked` (engellenen is)

Kurallar:
- `done` olduktan sonra module pack silinmez
- Dosya kalici audit belgesi olarak korunur
- Yeni degisiklikte ayni dosyada `Implementation Notes` guncellenir

## 8. Authority Rule

Yetki hiyerarsisi:

```text
Module Pack > Domain Config > AGENTS.md > .antigravity/
```

Ayni konuda celiski varsa module pack kazanir.

## 9. Quick Template

```md
---
id: MDM-001
name: Product Management
domain: master-data-management
status: draft
owner: ali.tufanoglu
branch: feature/mdm/mdm-001-product-management
started: 2026-04-20
target: 2026-05-05
---

# MDM-001 — Product Management

## Module Summary
...

## Ownership and Boundaries
...

## Repo Scope
...

## Protected Paths
...

## Dependencies
...

## Runtime Constraints
...

## Acceptance Criteria
- [ ] ...

## Test Expectations
- ...

## Implementation Notes
...

## Follow-up Items
...
```
