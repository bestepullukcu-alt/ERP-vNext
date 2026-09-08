'use strict';

/**
 * FIX-RBAC-PERM-MODULE-ATTRIBUTION — how one permission reads on the Role Permissions screen.
 *
 * The screen used to print one row per permission with the label "{Verb} · {Entity}" — 415 rows, each one
 * repeating the verb and, through its module group, its own module name. What a person actually looks for is
 * "which SOURCE, and what may I do with it": the source is the row, the verbs are chips on it. That turns 415
 * rows into 157 and lets the four actions on one entity be read in one glance instead of four.
 *
 * `split` is that decision, and it is PURE + DOM-FREE on purpose so it can be tested directly
 * (tests/role-assignments-perm-label.test.js). It returns CODES beside every label: the codes are what the
 * screen groups, filters and posts by — only the labels are language.
 *
 *   split({ module:'tasks', resource:'tasks.work-report', action:'read-tenant-wide', key:'…' }, L10N)
 *     → { entity: { code:'work-report',  label:'Çalışma Raporu' },
 *         action: { code:'read', label:'Görüntüle', tone:'info' },
 *         scope:  { code:'tenant-wide',  label:'Tüm kiracı' } }
 *
 * Three rules earn their keep:
 *  1. The MODULE is not repeated in the row. The group header already says it, so a resource that merely
 *     restates the module ("platform.tasks.read" → resource "tasks" inside module "tasks") yields an EMPTY
 *     entity code, and the caller labels that row with the module's own name.
 *  2. The SCOPE is its own chip, not part of the verb. "read-tenant-wide" is the read action at a wider reach,
 *     and reading it as a 98th distinct verb hides exactly the thing that makes it dangerous.
 *  3. Nothing is a fixed list of products. Labels come from the l10n bridge BY CODE and fall back to
 *     humanising the code, so a module that self-registers tomorrow reads as words without a code change —
 *     and a raw slug never reaches the screen.
 */
(function (global) {
    // Action → tone/glyph. Unknown actions get a neutral pair rather than no chip.
    const ACTION_TONE = {
        read: 'info', view: 'info', list: 'info', search: 'info', preview: 'info',
        create: 'primary', update: 'warning', edit: 'warning', manage: 'warning',
        delete: 'danger', 'bulk-delete': 'danger', archive: 'secondary', export: 'secondary'
    };

    /**
     * Action suffixes that name a REACH rather than an operation. Kept deliberately tiny and evidence-based:
     * the live catalog has exactly one today ("read-tenant-wide"). A suffix here is split off the action and
     * shown as its own chip; everything else stays part of the verb.
     */
    const SCOPE_SUFFIXES = ['tenant-wide'];

    const humanize = (value) => String(value ?? '')
        .split(/[-._\s]+/)
        .filter(Boolean)
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
        .join(' ');

    // Canonical spelling of a code, matching NavNameLocalizer.Normalize and ModuleLabel.normalize: uppercase,
    // non-alphanumerics dropped. The resx keys are authored in this form (Nav.Page.FIELDDEFINITIONS), and a
    // permission's raw segment is not ("field-definitions"), so the two only meet through this transform.
    const normalize = (value) => String(value ?? '').replace(/[^a-z0-9]/gi, '').toUpperCase();

    // Nested l10n maps are authored by code — verbs verbatim ({ read: 'Görüntüle' }), entity names canonical
    // ({ FIELDDEFINITIONS: 'Alan Tanımları' }). A miss humanizes the code rather than showing nothing, which is
    // what keeps this a BRIDGE and not a fixed product list: a module that ships tomorrow reads as words today.
    const lookup = (map, code) => {
        const hit = map && (map[code] ?? map[normalize(code)]);
        return typeof hit === 'string' && hit.trim().length > 0 ? hit.trim() : humanize(code);
    };

    /**
     * Is this resource segment the module saying its own name again? The key grammar spells the owner out in
     * several ways, and all of them are the module repeating itself:
     *   platform.tasks.field-definitions.manage   module "tasks"        segment "tasks"              — the same word
     *   platform.organization-units.read          module "organization" segment "organization-units" — a longer form
     *   crm.account.attribute.read                module "crm-account"  segment "account"            — a shorter form
     * A segment that merely SHARES a word is not an echo: "account-contact" inside "crm-contact" is its own thing.
     */
    const isModuleEcho = (segment, module) => {
        const seg = normalize(segment);
        const mod = normalize(module);
        if (!seg || !mod) return false;
        return seg === mod || mod.endsWith(seg) || seg.startsWith(mod);
    };

    /**
     * The SOURCE this permission is about, with the module's own name taken out of it — the group header says the
     * module once, and the row should not say it again. Returns '' when the permission is about the module itself,
     * which is the caller's cue to label the row with the module's own name.
     *
     *   module "tasks"        resource "tasks"                        → ''               (the module itself)
     *   module "tasks"        resource "tasks.field-definitions"      → 'field-definitions'
     *   module "work-report"  resource "tasks.work-report"            → ''  (the path ENDS at the module)
     *   module "crm"          resource "knowledge.concept"            → 'knowledge.concept' (real depth, kept)
     */
    const entityCode = (module, resource) => {
        const segments = String(resource ?? '').split('.').filter(Boolean);

        while (segments.length > 0 && isModuleEcho(segments[0], module)) {
            segments.shift();
        }
        if (segments.length === 0) return '';

        // The module can also sit at the END of the path (platform.tasks.work-report.* inside "work-report"):
        // the leading segments are namespace, and the thing the row is about is the module itself.
        if (isModuleEcho(segments[segments.length - 1], module)) return '';

        return segments.join('.');
    };

    const splitAction = (action) => {
        const raw = String(action ?? '').trim().toLowerCase();
        for (const suffix of SCOPE_SUFFIXES) {
            if (raw.endsWith('-' + suffix)) {
                return { action: raw.slice(0, -(suffix.length + 1)), scope: suffix };
            }
        }
        return { action: raw, scope: null };
    };

    /**
     * perm  { module, resource, action, key } — the catalog row, verbatim.
     * l10n  { ActionVerbs:{code:label}, EntityNames:{CODE:label}, ScopeLabels:{code:label} } — any part may be absent.
     */
    const split = (perm, l10n) => {
        const L = l10n || {};
        const p = perm || {};
        const parts = splitAction(p.action);
        const code = entityCode(p.module, p.resource);

        return {
            entity: {
                code,
                // '' means "this row IS the module" — the caller supplies the module's own label there, because
                // only the caller knows it (it comes from the nav/resx module map).
                label: code ? lookup(L.EntityNames, code) : ''
            },
            action: {
                code: parts.action,
                label: lookup(L.ActionVerbs, parts.action),
                tone: ACTION_TONE[parts.action] || 'secondary'
            },
            scope: parts.scope
                ? { code: parts.scope, label: lookup(L.ScopeLabels, parts.scope) }
                : null
        };
    };

    global.PermLabel = { split, humanize, normalize, ACTION_TONE, SCOPE_SUFFIXES };
})(typeof window !== 'undefined' ? window : globalThis);
