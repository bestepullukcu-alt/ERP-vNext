# Layered Agent + Domain Package Model — SOP (v2.1)

> [!IMPORTANT]
> Bu dosya projenin ana operasyonel standartlarını (SOP) tanımlar.
> Otorite sırası: `Module Pack > Domain Config > AGENTS.md > .antigravity/ > SOP refs`

## 1. Purpose
This SOP defines the operating model for using **ChatGPT**, **Codex**, **AGENTS.md**, **Global Engineering System (`.antigravity/`)**, **Domain Packages**, and **Module Execution Packages** together in the project.

The model is designed for teams that:
- decide **domain and module scope in ChatGPT**
- perform **development in Antigravity/Codex**
- execute work **module by module**
- need **stable engineering standards** without repeating the same context in every session

This SOP establishes a **layered control model** so that analysis, execution, and coding do not conflict.

---

## 2. Model Summary
The project uses a four-layer operating model:

1. **AGENTS.md** → Codex execution contract
2. **.antigravity/** → Global Engineering System (global reusable engineering layer)
3. **execution/domains/<domain>/** → domain execution package
4. **execution/domains/<domain>/module-packs/** → module execution / coding package

Archive material and transport bundles are **not authoritative**. They are reference, backup, or handoff artifacts only.

## 2.1 Terminology Alignment
Use the following semantic names consistently:
- **Global Engineering System** = `.antigravity/`
- **Domain Controls** = `execution/domains/<domain>/controls/`
- **Module Pack** = `execution/domains/<domain>/module-packs/`
- **Global Skills** = `.antigravity/skills/`

This keeps global reusable engineering capability separate from domain-specific execution controls.

---

## 3. Core Principles
1. **Repo-native execution**: live instructions must live inside the repo.
2. **Single source of truth**: coding instructions must not compete across multiple layers. Engineering standards (tech stack, patterns) belong ONLY in `.antigravity/rules/`.
3. **Domain separation**: each domain must have explicit boundaries.
4. **Module-first execution**: coding work is executed module by module.
5. **Stable standards**: All engineering rules (frontend, backend, security, etc.) are maintained in the **Global Engineering System** at `.antigravity/rules/`. Domain and module packs must REFER to these, not RESTATED them.
6. **No Redundancy**: Domain and module packs must avoid duplicating global rules to prevent version conflicts.
7. **Authority clarity**: the most specific layer wins.

---

## 4. Authority Hierarchy
The project must use the following authority order.

1. **Module Execution Package**
2. **Domain Package**
3. **AGENTS.md**
4. **Global Engineering System (`.antigravity/`)**
5. **Archive / external reference**

### Interpretation
- Module-level instructions override broader domain guidance where the module requires specificity.
- Domain-level instructions override generic repo assumptions.
- `AGENTS.md` governs Codex behavior at repo level.
- the Global Engineering System in `.antigravity/` provides reusable standards, workflows, and skills.

---

## 5. Repository Structure
The Diten ERP vNext structure is mandatory:

```text
ERP-vNext/
├── AGENTS.md
├── .antigravity/
│   ├── ARCHITECTURE.md
│   ├── PROMPT-GUIDE.md
│   ├── agents/
│   ├── workflows/
│   ├── rules/
│   └── skills/
├── execution/
│   └── domains/
│       ├── master-data-management/
│       │   ├── README.md
│       │   ├── domain-config.md
│       │   ├── decisions/
│       │   ├── controls/
│       │   └── module-packs/
│       │       ├── MDM-001-currency-management.md
│       │       └── ...
│       ├── platform-shared-services/
│       └── enterprise-strategy-business-performance/
├── services/
│   ├── DitenMdmService/
│   ├── DitenAuthService/
│   └── ...
├── frontend/
│   └── Diten.Web/
└── gateway/
    └── DitenApiGateway/
```

### Notes
- `execution/domains/` is the **live execution workspace**.
- `module-packs/` is the **live coding workspace** for each module.
- `batches/` and `snapshots/` layers are not used in this project; phase orchestration is handled by `.antigravity/workflows/add-module.md`.

---

## 6. Layer Responsibilities

### 6.1 AGENTS.md — Codex Execution Contract
**Purpose:** repo-wide execution guardrails for Codex.

**Must contain:**
- concrete repo paths
- protected paths
- build/test commands
- runtime decisions that affect coding
- references to authoritative domain packages

---

### 6.2 .antigravity/ — Global Engineering System
**Purpose:** reusable global engineering system across all domains.

**Contains:**
- core agents
- readiness/scope analysis agents
- workflows
- coding rules
- frontend/backend standards
- skills
- architecture and prompting guidance

---

### 6.3 execution/domains/<domain>/ — Domain Package
**Purpose:** execution layer for one domain.

**Contains:**
- domain scope
- domain boundaries
- in-scope / out-of-scope modules
- protected paths relevant to the domain
- runtime decisions
- domain controls
- domain-level notes

---

### 6.4 execution/domains/<domain>/module-packs/ — Module Execution Package
**Purpose:** coding layer for one module.

Each module pack defines exactly what is needed to implement one module correctly.

**Minimum content for each module pack (YAML Frontmatter is required):**
- id, name, domain, status, owner, branch, dates
- purpose
- owned objects
- in-scope / out-of-scope
- dependencies
- repo scope
- protected paths
- acceptance criteria
- test expectations

**File model:** one file per module following `{DOMAIN}-{NNN}-{slug}.md` naming.

---

## 7. Operational Workflow

### Step 1 — Decide Domain and Module
Decide:
- which domain is in scope (MDM, PSS, or ESBP)
- which module is next
- what the scope/AC is

### Step 2 — Update the Domain Package
Verify/update:
- `README.md`
- `domain-config.md`
- `decisions/`

### Step 3 — Prepare the Module Pack
Create the module pack file with Mandatory YAML Frontmatter in `execution/domains/{domain}/module-packs/`.

### Step 4 — Run Strategy & Readiness
Use `/add-module` workflow for phase-based execution.

### Step 5 — Execute in Codex
Codex must use:
- `AGENTS.md`
- `.antigravity/`
- the selected domain package
- the selected module pack

### Step 6 — Review and Persist
Review outputs and update module notes/decisions.

---

## 8. Domain Package Contents

### 8.1 README.md
Short business and technical summary of the domain.

### 8.2 domain-config.md
Operational summary of the domain.

### 8.3 decisions/
Store explicit domain decisions (runtime, ownership, deferred).

### 8.4 controls/
Shared domain setup/control docs.

---

## 9. Module Pack Contents
**Mandatory YAML Frontmatter:**
```yaml
id: MDM-001
name: Currency Management
domain: master-data-management
status: draft
owner: ai-agent
branch: feature/mdm/mdm-001-currency-management
```

---

## 10. Domain vs Module Separation Rules
Preserve this split:
- **Domain:** shared boundaries, common decisions, domain controls.
- **Module:** specific implementation target, coding constraints, AC.

---

## 11. Batch Usage Policy (Not Applicable)
This project uses **Workflow-based execution** via `.antigravity/workflows/` instead of legacy batch prompts.

---

## 12. Governance Rules
1. Always start coding from repo root to load `AGENTS.md`.
2. Domain and module decisions must be explicit.
3. Protected paths must be visible.
4. Acceptance criteria must be testable.
5. Development is module-based.

---

## 13. Starter Set
- `AGENTS.md`
- `.antigravity/` (rules, workflows, agents)
- `execution/domains/` (MDM, PSS, ESBP)

---

## 14. Final Operating Rule
- **ChatGPT** decides domain and module.
- **Antigravity** provides global engineering system.
- **Domain package** provides execution context.
- **Module pack** provides live coding context.
- **Codex** executes with the correctly ordered layers.
