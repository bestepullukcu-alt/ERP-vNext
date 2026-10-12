/**
 * Company selector — pack §19 waiver W-0173-02 (see Views/Inventory/Shared/_CompanySelector.cshtml).
 *
 * The chosen legal entity is kept in a same-origin cookie whose name the server derived from the user (so a second
 * user on this browser does not inherit it). The value is the legal-entity id only — no name, no other data. A change
 * reloads the page: every list, KPI and action of the inventory screens is scoped by the server to that company, and
 * the next page (S-1 ↔ S-2) reads the same cookie.
 */
'use strict';

// Bound after DOMContentLoaded: this script sits in the page body (the selector partial) and runs before the shell's
// jQuery has loaded; binding later lets jQuery's .on see both native and jQuery-raised change events.
document.addEventListener('DOMContentLoaded', function () {
    const root = document.getElementById('inventory-company-selector');
    const select = document.getElementById('inventoryCompany');
    if (!root || !select) return;

    const cookieName = root.getAttribute('data-company-cookie');
    if (!cookieName) return;

    const isUuid = (value) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value || '');

    const choose = () => {
        const value = select.value;
        if (!isUuid(value)) return;
        // One year; SameSite=Lax; path=/ so S-1 and S-2 share it. Not a secret: MOD-0173 verifies it on every request.
        document.cookie = `${cookieName}=${encodeURIComponent(value)}; path=/; max-age=31536000; SameSite=Lax`;
        window.location.reload();
    };

    // jQuery's .on also sees a change raised through jQuery (e.g. if a theme enhances the select); native otherwise.
    if (window.jQuery) window.jQuery(select).on('change', choose);
    else select.addEventListener('change', choose);
});
