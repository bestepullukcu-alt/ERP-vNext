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
    /*
     * Action → tone + glyph, by FAMILY rather than by an exact-match list.
     *
     * The exact-match list this replaces knew 13 verbs. The live catalog has 97, so 84 of them — 135 permissions —
     * rendered as the same grey cog: "Ata", "Toplu Sil", "İptal Et" and "Complete" were visually identical, which
     * is worse than no colour at all, because it says they are the same kind of thing. Listing 84 more verbs would
     * only move the cliff: the next self-registered module brings its own.
     *
     * A family is matched against the action's WORDS, not the whole string. That is the part that earns its keep:
     * `assign-role`, `assign-roles` and `assign-permission` are all the assign family, and an exact-match list
     * missed every one of them. Words are scanned in order, first hit wins — so `bulk-delete` lands on `delete`
     * (danger) and `view_status_history` on `view` (info).
     *
     * Family words are single words on purpose. The danger family is written `delete, redact, invalidate, purge`
     * rather than `bulk-delete, redact-actor`: the qualifier is the part that varies, the verb is the part that
     * classifies. An unmatched action stays neutral AND keeps the cog — the cog now means "no family", not
     * "not on the list", which is a much smaller and self-limiting set.
     */
    const ACTION_FAMILIES = [
        { tone: 'info',      icon: 'bx-show',         words: ['read', 'view', 'list', 'search', 'preview', 'compare', 'audit', 'review', 'verify', 'validate'] },
        { tone: 'primary',   icon: 'bx-plus',         words: ['create', 'instantiate', 'register', 'append', 'record', 'request', 'upload'] },
        { tone: 'warning',   icon: 'bx-edit',         words: ['update', 'edit', 'manage', 'configure', 'move', 'correct', 'rebase', 'change', 'reconcile'] },
        { tone: 'danger',    icon: 'bx-trash',        words: ['delete', 'redact', 'invalidate', 'purge'] },
        { tone: 'success',   icon: 'bx-check-circle', words: ['approve', 'publish', 'activate', 'reactivate', 'release', 'complete', 'confirm', 'sign', 'submit', 'start'] },
        { tone: 'secondary', icon: 'bx-undo',         words: ['cancel', 'reject', 'suspend', 'deprecate', 'retire', 'expire', 'archive', 'revoke'] },
        { tone: 'dark',      icon: 'bx-user-check',   words: ['assign', 'claim', 'delegate', 'allocate', 'reserve', 'share'] },
        { tone: 'primary',   icon: 'bx-play-circle',  words: ['run', 'execute', 'retry', 'initialize', 'evaluate'] }
    ];

    /*
     * ⚠ The verbs added above (review, verify, validate, record, request, change, reconcile) close a gap the first
     * delivery left: six of the nine verbs that had just earned a curated translation still resolved to no family,
     * so they read as words and were drawn as the generic cog. A verb common enough to be worth translating in
     * seven languages is common enough to be worth classifying -- the two lists must not drift apart, and
     * `every curated verb resolves to a family` in the tests is what keeps them together.
     *
     * `change` rather than `change-lifecycle`: resolveFamily scans word by word, so the one entry also catches
     * change-status, and any change-* a future module mints.
     *
     * The eighth family (run / execute / retry / initialize / evaluate) exists because five verbs kept arriving with
     * no home, which is what this file's own note says a missing family looks like -- they neither read, create,
     * change, destroy, advance, reverse nor hand over: they SET SOMETHING GOING. It shares the create tone and
     * carries its own glyph, the same split export/import already uses: the tone says how consequential, the icon
     * says which verb.
     */
    const UNFAMILIAR = { tone: 'secondary', icon: 'bx-cog' };

    /*
     * The small exception map the family rule is allowed to have — and it is icon-only on purpose.
     *
     * `export` and `import` do not belong to any of the seven families: they neither read, create, change, destroy,
     * advance a lifecycle, reverse one, nor hand over ownership — they move data across the boundary. Their TONE is
     * therefore correctly the neutral one. But they carried their own glyphs before this change, and dropping them
     * into the generic cog would have been a regression introduced by a refactor, which is the one thing a refactor
     * may not do. So the tone comes from the rule and only the glyph is pinned here.
     *
     * Keep this map tiny. A verb that keeps arriving here is telling you a family is missing, not that the map
     * should grow.
     */
    const ACTION_ICON_EXCEPTIONS = { export: 'bx-export', import: 'bx-import' };

    /** The family an action belongs to, or null. Words are scanned in order so a qualifier never outranks the verb. */
    const resolveFamily = (actionCode) => {
        const code = String(actionCode ?? '');
        const words = code.split(/[-._]/).filter(Boolean);
        for (const word of words) {
            const family = ACTION_FAMILIES.find((f) => f.words.includes(word));
            if (family) return family;
        }

        const icon = ACTION_ICON_EXCEPTIONS[code];
        return icon ? { tone: UNFAMILIAR.tone, icon } : null;
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

    /*
     * DISPLAY normalization for an action code: lowercase, and `_` folded to `-`.
     *
     * Measured in the live catalog: the same verb is stored in four spellings. The BRD seed passes PascalCase to
     * the constructor (`Action = "Read"`, while the Key is lowercased), MOD-0251 uses snake_case
     * (`view_sensitive`, `change_status`), and everything else uses kebab. Nine verbs collide that way — `read/Read`,
     * `create/Create`, `update/Update`, `approve/Approve`, `publish/Publish`, `submit/Submit`, `preview/Preview`,
     * `validate/Validate`, and `lookup-validation/lookup_validation`. Without this, `Read` misses the verb bridge
     * and prints "Read" next to a sibling row's "Görüntüle", and misses its family so it renders grey.
     *
     * ⚠ This is a DISPLAY fold and nothing else. `Permission.Key`, the row's data attributes, the filter and the
     * assignment call all keep the catalog's own spelling — ADR-001 §1 froze the key. It is also NOT a merge: no
     * row carries two permissions with the same normalized action (measured: zero), so two chips never become one.
     * The real defect is in the seed, tracked as BL-344; this only stops it from reaching the reader's eye.
     *
     * Separate from `normalize` above on purpose: that one produces the CANONICAL RESX KEY form (uppercase,
     * alphanumerics only) and folding them together would break the entity-name lookup.
     */
    const normalizeAction = (action) => String(action ?? '').trim().toLowerCase().replace(/_/g, '-');

    const splitAction = (action) => {
        const raw = normalizeAction(action);
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
            action: (() => {
                const family = resolveFamily(parts.action) || UNFAMILIAR;
                return {
                    code: parts.action,
                    label: lookup(L.ActionVerbs, parts.action),
                    tone: family.tone,
                    icon: family.icon
                };
            })(),
            scope: parts.scope
                ? { code: parts.scope, label: lookup(L.ScopeLabels, parts.scope) }
                : null
        };
    };

    global.PermLabel = { split, humanize, normalize, normalizeAction, resolveFamily, ACTION_FAMILIES, ACTION_ICON_EXCEPTIONS, UNFAMILIAR, SCOPE_SUFFIXES };
})(typeof window !== 'undefined' ? window : globalThis);
