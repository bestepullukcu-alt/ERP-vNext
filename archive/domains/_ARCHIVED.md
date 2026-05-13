---
status: ARCHIVED
archived_on: 2026-05-13
reason: Redundant with AGENTS.md + .antigravity/rules/ + docs/platform/master-plan.md
---

# Archived Domain Layers

Bu klasör, eski SOP (Layered Agent + Domain Package Model v2.1) altında üretilen ama mevcut module-first execution akışında otorite taşımayan domain katmanlarını barındırır:

- `*/controls/` — domain-level execution controls (engineering kuralları artık `.antigravity/rules/`'de)
- `*/decisions/` — runtime / ownership / deferred kararları (artık `AGENTS.md` + `docs/platform/master-plan.md`'de)

## Otorite hiyerarşisi (güncel)

1. Module Pack — `execution/domains/{domain}/module-packs/{ID}.md`
2. Domain Config — `execution/domains/{domain}/domain-config.md`
3. `AGENTS.md` (repo root)
4. `.antigravity/rules/` (engineering NASIL)
5. `docs/platform/master-plan.md` (Platform/Admin envanteri + MVP scope + cross-cutting standartlar)

Arşivdeki dosyalar **referans değildir**, sadece tarihsel kayıttır.
