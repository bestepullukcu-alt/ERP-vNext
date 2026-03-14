---
description: Debugging command. Activates DEBUG mode for systematic problem investigation in Diten ERP vNext.
---

# /debug - Systematic Problem Investigation (Diten Edition)

$ARGUMENTS

---

## Purpose
This command activates DEBUG mode for systematic investigation of issues, errors, or unexpected behavior, specifically aligned with Diten ERP vNext Architecture.

---

## 🛠️ Diten-Specific Checkpoints
When debugging in this project, these 5 pillars MUST be checked first:
1. **Multi-Tenancy:** Is `X-Tenant-Id` GUID present? Is the Repository filtering correctly?
2. **Localization:** Is the `window.L10n` bridge populated? Are keys missing in any of the 8 `.resx` files?
3. **CQRS Structure:** Is the logic in the correct `Handlers/` subfolder?
4. **Networking:** Is the route 100% lowercase? Does `Location` header point to Gateway?
5. **MongoDB Runtime:** Is MongoDB listening on `27017`? If not, services may return `500` / timeout and UI (DataTables) may stay in loading/skeleton state.

---

## Behavior

1. **Gather information**
   - Error message + **CorrelationId** (from logs).
   - Tenant context (Which TenantId is affected?).
   - Recent changes in `.antigravity/rules`.

2. **Form hypotheses**
   - List possible causes (e.g., Tenant mismatch, L10n key missing, Mongo Index missing).

3. **Investigate systematically**
   - Check logs via **Logging & Observability** standard.
   - Use **Explorer** to verify file paths vs. **Views Organization** rules.

4. **Fix and prevent**
   - Apply fix.
   - **Important:** Ensure the fix doesn't break the 8-language synchronization.

---

## Output Format

```markdown
## 🔍 Debug: [Issue Name]

### 1. Symptom & Context
- **What:** [Description]
- **Tenant affected:** [Tenant GUID or All]
- **CorrelationId:** `[ID from logs]`

### 2. Information Gathered
- Error: `[error message]`
- File: `[filepath]`
- Standards Violation: [e.g., MOD-0013, WORKFLOW-001]

### 3. Hypotheses
1. ❓ [High probability - e.g., Tenant Filter missing]
2. ❓ [Second possibility - e.g., L10n Bridge failure]

### 4. Investigation Result
[What I checked] → [Found X]

### 5. Root Cause
🎯 **[Why it happened - e.g., Missing ITenantDocument on Entity]**

### 6. Fix
[Before/After code blocks]

### 7. Prevention
🛡️ [How to prevent - e.g., Added check to mongo-index.md]
