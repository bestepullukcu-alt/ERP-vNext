# Domain and Module Preparation Rules

> [!IMPORTANT]
> Bu dosya projenin **Domain** ve **Modül** hazırlık kurallarını tanımlar.
> Otorite sırası: `Module Pack > Domain Config > AGENTS.md > .antigravity/ > SOP refs`

## Purpose
These rules define how Domain and Module parts should be prepared before development starts. The goal is to ensure:
- domain context is prepared once and reused correctly,
- module context is prepared as a focused coding package,
- Codex and developers can start implementation with minimal ambiguity.

## Terminology Alignment
- **Global Engineering System** = `.antigravity/`
- **Domain Controls** = `execution/domains/<domain>/controls/`
- **Module Pack** = `execution/domains/<domain>/module-packs/`

---

## 1. Core Principle
Preparation must happen in **two distinct layers**:
- **Domain part** = execution context for a domain (shared frame).
- **Module part** = coding context for one concrete module (implementation unit).

---

## 2. Rules for Preparing the Domain Part

## 2.1 Purpose of the Domain Part
The Domain part defines:
- What this domain owns/does not own
- Which modules belong to this domain
- Shared dependencies and protected paths
- Fixed runtime decisions

---

## 2.2 Mandatory Domain Content
- Domain name and purpose
- In-scope / Out-of-scope modules (Logical scope)
- Ownership boundaries (Business ownership)
- Shared dependencies (Service-to-service)
- Protected paths (Domain-specific)
- **Note:** Do NOT include tech stack, architecture or global coding rules here. These are inherited from `.antigravity/rules/`.

---

## 2.3 Domain Deliverables
- `README.md`
- `domain-config.md`
- `decisions/ownership-decisions.md` (Business ownership logic)
- `decisions/deferred-items.md` (Postponed features/logic)
- `controls/` (Domain-specific business validations)

---

## 3. Rules for Preparing the Module Part

## 3.1 Purpose of the Module Part
The Module part answers:
- What exactly is being built now
- Which objects this module owns
- Repo scope and protected paths
- Definition of done (Acceptance Criteria)

---

## 3.2 Mandatory Module Content
**Mandatory YAML Frontmatter:**
- id, name, domain, status, owner, branch, started, target

**Body Content:**
- Module purpose
- Owned objects
- In-scope / Out-of-scope
- Repo scope
- Protected paths
- Acceptance criteria (testable)
- Test expectations

---

## 4. Separation Rules: Domain vs Module
- **Domain:** boundaries, common decisions, shared standards, domain-level scope.
- **Module:** implementation target, specific repo scope, acceptance criteria.

---

## 5. Preparation Workflow
1. **Define the Domain:** Prepare purpose, boundaries, and runtime decisions.
2. **Define the Module:** Prepare YAML frontmatter, objective, repo scope, and AC.
3. **Validate Alignment:** Ensure the module does not violate domain boundaries.

---

## 6. Naming Rules

## 6.1 Domain Naming
Use normalized names: `master-data-management`, `platform-shared-services`, etc.

## 6.2 Module Naming
Use the format: `{DOMAIN-SHORT}-{NNN}-{slug}`
- Example: `MDM-001-currency-management`
- Example: `PSS-001-identity-access`

---

## 7. Authority Rules
1. **Module package**
2. **Domain package**
3. **AGENTS.md**
4. **Global Engineering System (`.antigravity/`)**

---

## 8. Final Operating Rule
- The **Domain part** explains **how this domain works**.
- The **Module part** explains **what to build now**.
