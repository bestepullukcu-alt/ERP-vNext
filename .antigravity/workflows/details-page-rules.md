---
description: [Details Page UI Layout Rules]
---
# Details (Detayları Gör) Page UI Rules

When creating or modifying a "Read-Only Details" view for a record, you MUST choose between two distinct patterns. Both patterns should never be built as default states in the SAME full-page simultaneously. Follow the capacity rules below:

---

## RULE #1: Choice of Pattern & Capacity

### Pattern A: Offcanvas "Quick View" (For Lightweight Data)
**When to use:** If the record has a small amount of detail mostly fitting a few fields (e.g. 5-10 short properties) and NO complex sub-lists or deep tabs.
- **Trigger:** Rendered directly on the List/Index page (e.g. clicking "Quick Preview" from the DataTable row action).
- **Structure:** Use the Bootstrap Offcanvas component sliding from the right (`offcanvas-end` with `width: 480px`).
- **Footer action:** Include a "Full Details" button in the offcanvas if there is a more detailed dedicated page.

### Pattern B: Isolated Full Details Page (For Heavy Data)
**When to use:** If the record contains heavily nested relationships, many tabs, or categorized property blocks (like Legal Entities with General Info, Contact, Financials).
- **Trigger:** Navigating to `/{Controller}/Details/{id}`.

*(If you chose Pattern B, you MUST apply rules 2 through 5 below.)*

---

## RULE #2: Removing Left User/Profile Card
- Do NOT use a split layout with a narrow left-hand user/avatar profile card. 
- The content container for details should be `col-12` (full width), displaying cards in a unified grid structure.
- Redundant data (e.g., repeating contact info in a sidebar when it exists in a main tab) must be eliminated.

## RULE #3: Header and Dynamic Description
- The header should have a dynamic and useful sub-description (`<p class="mb-0">`), not just "Details".
- The description should be built cleanly using a List of string elements joined by a bullet point (`&bull;` or `•`).
  - Example logic: 
    ```csharp
    @{
        var descParts = new List<string>();
        if(!string.IsNullOrEmpty(Model.Type)) { descParts.Add(Model.Type); }
        if(!string.IsNullOrEmpty(Model.Number)) { descParts.Add("No: " + Model.Number); }
    }
    <p class="mb-0">@(string.Join(" • ", descParts))</p>
    ```

## RULE #4: Grid Row Structure (N-Card Layout)
- The main read-only data must be grouped logically into distinct cards (e.g., General Info, Contact, Financial).
- These cards must be wrapped in a Bootstrap grid container using `row g-4`.
- Each individual card should sit inside a responsive column layer, specifically `<div class="col-12 col-md-6 col-lg-4">`.
- This ensures that 3 layout cards will horizontally align on wide screens (`col-lg-4`) and stack beautifully on smaller screens (`col-12`).

## RULE #5: Vertical Stack inside Information Cards
- Data Lists inside the cards (`<dl class="row mb-0">`) must use vertical stacking (top-to-bottom) for their labels and values because the cards are narrow on a 3-column layout. 
- Do NOT use side-by-side structures like `col-sm-4` / `col-sm-8`.
- ALWAYS use the following pattern:
  - `<dt class="col-12 fw-medium text-heading mb-1">Label</dt>`
  - `<dd class="col-12 mb-4">Value</dd>`
