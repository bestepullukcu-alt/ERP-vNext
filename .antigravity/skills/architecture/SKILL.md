---
name: architecture
description: Architectural decision-making framework. Requirements analysis, trade-off evaluation, ADR documentation. Use when making architecture decisions or analyzing system design.
allowed-tools: Read, Glob, Grep
---

# Architecture Decision Framework

> "Requirements drive architecture. Trade-offs inform decisions. ADRs capture rationale."

## 🏢 Proje Bağlamı — Diten ERP vNext (önce bunu oku)

Bu skill genel bir mimari karar çerçevesidir. Diten ERP vNext'te aşağıdaki kararlar **verilmiştir ve bu skill ile yeniden tartışılmaz**:

- **Mikroservis** mimarisi (`services/Diten.*Service`, Ocelot Gateway üzerinden erişim) — `AGENTS.md` §2–§3, `erp-architecture.md`
- **5 katman** (Api / Application / Domain / Persistence / Infrastructure) + **CQRS** (MediatR) — `AGENTS.md` §6
- **Repository** deseni + MongoDB; kiracı (`TenantId`) filtresi depo katmanında uygulanır — `erp-architecture.md`, `repository-standard.md`, `multi-tenancy.md`

Bu skill'in dosyalarındaki "önce monolit", "tek model", "basit CRUD için repository gereksiz", "ORM'e doğrudan erişim" gibi öneriler bu projede **uygulanmaz**. Skill'i yalnız bu sınırlar içindeki yeni kararlar (ör. bir servis içi desen seçimi, ADR yazımı) için kullan. Sabit bir kararın değişmesi gerektiğini düşünüyorsan uygulamaya geçme; kararı Control Tower / Enterprise Architect'e götür.

## 🎯 Selective Reading Rule

**Read ONLY files relevant to the request!** Check the content map, find what you need.

| File | Description | When to Read |
|------|-------------|--------------|
| `context-discovery.md` | Questions to ask, project classification | Starting architecture design |
| `trade-off-analysis.md` | ADR templates, trade-off framework | Documenting decisions |
| `pattern-selection.md` | Decision trees, anti-patterns | Choosing patterns |
| `examples.md` | MVP, SaaS, Enterprise examples | Reference implementations |
| `patterns-reference.md` | Quick lookup for patterns | Pattern comparison |

---

## 🔗 Related Skills

| Skill | Use For |
|-------|---------|
| `@[skills/database-design]` | Database schema design |
| `@[skills/api-patterns]` | API design patterns |

---

## Core Principle

**"Simplicity is the ultimate sophistication."**

- Start simple
- Add complexity ONLY when proven necessary
- You can always add patterns later
- Removing complexity is MUCH harder than adding it

---

## Validation Checklist

Before finalizing architecture:

- [ ] Requirements clearly understood
- [ ] Constraints identified
- [ ] Each decision has trade-off analysis
- [ ] Simpler alternatives considered
- [ ] ADRs written for significant decisions
- [ ] Team expertise matches chosen patterns
