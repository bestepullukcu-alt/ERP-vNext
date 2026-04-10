# Entity Versioning Standard (Immutable Revisions)

This rule defines the standard architecture for entities requiring an audit trail or lifecycle management (e.g., Compositions, SKUs, Specifications).

## 🏛️ Domain Architecture

Entities using this pattern MUST be split into İKİ parts:

1. **Header Entity (Header):**
   - Contains immutable identity (Code, Name).
   - Tracks the current lifecycle state (e.g., Active, Obsolete).
   - Points to the `CurrentVersionId`.
   - Inherits from `EntityBase`.

2. **Version Entity (Revision):**
   - Contains the payload/data (Formulation, Components, Values).
   - Tracks its own status (Draft, Active, Superseded, Obsolete).
   - Increments `VersionNo` (v1, v2, v3...).
   - Stores `EffectiveFrom` timestamps.

## 🔄 Lifecycle Transitions

| State | Edit Behavior | Transition |
|-------|---------------|------------|
| **Draft** | In-place update. | Can be promoted to `Active`. |
| **Active** | Creating new revision. | Becomes `Superseded` when a new version is activated. |
| **Superseded** | Read-only. | History for audit purposes. |
| **Obsolete** | Read-only. | Deactivated/End-of-life. |

## 🕹️ Application Logic (CQRS)

- **Update Command:** MUST check the status of the current version. If `Active`, it MUST create a new `Draft` version instead of editing the existing one.
- **Activate Command:** MUST mark the previous `Active` version as `Superseded` and update the Header's `CurrentVersionId`.

## 🎨 Frontend Standard

- **Details View:** MUST include a "Revision History" sidebar or list allowing users to view previous snapshots.
- **Badges:** 
    - `Draft`: `bg-label-warning`
    - `Active`: `bg-label-success`
    - `Superseded`: `bg-label-info`
    - `Obsolete`: `bg-label-danger`
