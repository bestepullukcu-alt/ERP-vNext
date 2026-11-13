/**
 * WP-VW-W6 · W6-WEB-a — the tenant's pre-order settings form (MOD-0168 §4.3). Reads the settings and the selling
 * companies (MDM; their countries feed the tier rows), writes them back with the version it read (a moved version or a
 * locked prefix is the CRM's 409, said in words; the fields stay as typed). Read only without the update key (the form
 * has no save button then and the proxy refuses the write anyway). The ranges are the CRM's (12–168 h, 0–10, 2–5 letters).
 */
'use strict';

const PreOrderSettingsForm = (function () {
    const U = window.PreOrdersUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('po-root');
    const api = '/Commercial/PreOrderSettings/api';
    const TIERS = ['pharmacist-purchase', 'depot', 'public'];
    const canUpdate = () => !!root && root.getAttribute('data-can-update') === 'true';
    let settings = null;
    let companies = [];

    const alertBox = (kind, text) => { const a = byId('po-alert'); a.className = 'alert alert-' + kind + (text ? '' : ' d-none'); a.textContent = text || ''; };
    const dis = () => (canUpdate() ? '' : ' disabled');

    const countries = () => Array.from(new Set(companies.map((c) => c.countryCode).filter(Boolean))).sort();
    const tierRow = (t) => '<div class="d-flex gap-2 align-items-center po-tier-row">'
        + '<div class="diten-field flex-fill"><i class="bx bx-flag diten-field-icon" aria-hidden="true"></i><select class="form-select po-tier-country" aria-label="' + U.esc(L().CountryLabel) + '"' + dis() + '>'
        + Array.from(new Set(countries().concat(t.countryCode ? [t.countryCode] : []))).sort().map((c) => '<option value="' + U.esc(c) + '"' + (c === t.countryCode ? ' selected' : '') + '>' + U.esc(c) + '</option>').join('') + '</select></div>'
        + '<div class="diten-field flex-fill"><i class="bx bx-purchase-tag-alt diten-field-icon" aria-hidden="true"></i><select class="form-select po-tier-tier" aria-label="' + U.esc(L().TierLabel) + '"' + dis() + '>'
        + TIERS.map((x) => '<option value="' + x + '"' + (x === t.tier ? ' selected' : '') + '>' + U.esc(L()['Tier_' + x.replace(/-/g, '_')] || x) + '</option>').join('') + '</select></div>'
        + (canUpdate() ? '<button type="button" class="btn btn-sm btn-icon btn-label-danger po-row-remove" aria-label="' + U.esc(L().RemoveRow) + '" title="' + U.esc(L().RemoveRow) + '"><i class="bx bx-trash"></i></button>' : '') + '</div>';
    const prefixRow = (p) => {
        const known = companies.some((c) => c.id === p.legalEntityId);
        const options = companies.map((c) => '<option value="' + U.esc(c.id) + '"' + (c.id === p.legalEntityId ? ' selected' : '') + '>' + U.esc(c.name + (c.countryCode ? ' · ' + c.countryCode : '')) + '</option>').join('')
            + (p.legalEntityId && !known ? '<option value="' + U.esc(p.legalEntityId) + '" selected>' + U.esc(p.legalEntityId) + '</option>' : '');
        return '<div class="d-flex gap-2 align-items-center po-prefix-row">'
            + '<div class="diten-field flex-fill"><i class="bx bx-buildings diten-field-icon" aria-hidden="true"></i><select class="form-select po-prefix-company" aria-label="' + U.esc(L().CompanyLabel) + '"' + dis() + '><option value=""></option>' + options + '</select></div>'
            + '<div class="diten-field po-prefix-code"><i class="bx bx-hash diten-field-icon" aria-hidden="true"></i><input type="text" class="form-control text-uppercase po-prefix-value" maxlength="5" aria-label="' + U.esc(L().PrefixLabel) + '" value="' + U.esc(p.prefix || '') + '"' + dis() + ' /></div>'
            + (canUpdate() ? '<button type="button" class="btn btn-sm btn-icon btn-label-danger po-row-remove" aria-label="' + U.esc(L().RemoveRow) + '" title="' + U.esc(L().RemoveRow) + '"><i class="bx bx-trash"></i></button>' : '') + '</div>';
    };

    const fill = () => {
        const s = settings || {};
        (document.querySelector('input[name="po-mode"][value="' + (s.approvalMode || 'always') + '"]') || {}).checked = true;
        byId('po-ttl').value = s.approvalLinkTtlHours == null ? 48 : s.approvalLinkTtlHours;
        byId('po-retry').value = s.transmissionRetryMax == null ? 5 : s.transmissionRetryMax;
        byId('po-tiers').innerHTML = (s.priceTierByCountry || []).map(tierRow).join('') || '<p class="text-muted small mb-0 po-empty-rows">' + U.esc(L().TierNone) + '</p>';
        byId('po-prefixes').innerHTML = (s.companyPrefixes || []).map(prefixRow).join('') || '<p class="text-muted small mb-0 po-empty-rows">' + U.esc(L().PrefixNone) + '</p>';
        byId('po-settings-meta').innerHTML = s.isDefault ? U.esc(L().SettingsDefault) : (s.updatedAt ? U.Th('SettingsUpdated', s.updatedBy || '', U.moment(s.updatedAt)) : '');
    };

    const read = () => ({
        approvalMode: (document.querySelector('input[name="po-mode"]:checked') || {}).value || 'always',
        approvalLinkTtlHours: Number(byId('po-ttl').value),
        transmissionRetryMax: Number(byId('po-retry').value),
        priceTierByCountry: Array.from(document.querySelectorAll('.po-tier-row')).map((r) => ({ countryCode: r.querySelector('.po-tier-country').value, tier: r.querySelector('.po-tier-tier').value })).filter((x) => x.countryCode),
        companyPrefixes: Array.from(document.querySelectorAll('.po-prefix-row')).map((r) => ({ legalEntityId: r.querySelector('.po-prefix-company').value || null, prefix: r.querySelector('.po-prefix-value').value.trim().toUpperCase() })).filter((x) => x.legalEntityId || x.prefix),
        expectedVersion: settings && !settings.isDefault ? settings.version : null
    });

    /** The CRM's ranges, checked before the save (the CRM checks them again): the first problem's key, or null. */
    const problem = (b) => {
        if (!Number.isInteger(b.approvalLinkTtlHours) || b.approvalLinkTtlHours < 12 || b.approvalLinkTtlHours > 168) return 'ErrTtl';
        if (!Number.isInteger(b.transmissionRetryMax) || b.transmissionRetryMax < 0 || b.transmissionRetryMax > 10) return 'ErrRetry';
        if (b.companyPrefixes.some((p) => !p.legalEntityId || !/^[A-Z]{2,5}$/.test(p.prefix))) return 'ErrPrefix';
        const seen = new Set();
        if (b.priceTierByCountry.some((t) => (seen.has(t.countryCode) ? true : (seen.add(t.countryCode), false)))) return 'ErrTierDuplicate';
        return null;
    };

    const save = async (event) => {
        event.preventDefault();
        if (!canUpdate()) return;
        const body = read();
        const bad = problem(body);
        if (bad) { alertBox('danger', L()[bad]); return; }
        const btn = byId('po-settings-save');
        btn.disabled = true;
        const r = await U.request('PUT', api + '/settings', body);
        btn.disabled = false;
        if (!r.ok) { alertBox('danger', r.status === 409 && !window.CommercialRules?.has(window.PreOrdersCore.errorCode(r.body)) ? L().Err_conflict : U.errorText(r)); return; }
        settings = U.data(r);
        fill();
        alertBox('success', L().SettingsSaved);
    };

    const init = async () => {
        if (!root || !U) return;
        const [s, c] = await Promise.all([U.request('GET', api + '/settings'), U.request('GET', api + '/legal-entities')]);
        companies = ((U.data(c) || {}).items || []).map((x) => ({ id: x.id, name: x.name, countryCode: x.countryCode }));
        settings = U.data(s);
        if (!settings) { alertBox('danger', U.errorText(s)); return; }
        fill();
        byId('po-settings-form').addEventListener('submit', save);
        byId('po-add-tier')?.addEventListener('click', () => { byId('po-tiers').querySelector('.po-empty-rows')?.remove(); byId('po-tiers').insertAdjacentHTML('beforeend', tierRow({ countryCode: countries()[0] || '', tier: 'pharmacist-purchase' })); });
        byId('po-add-prefix')?.addEventListener('click', () => { byId('po-prefixes').querySelector('.po-empty-rows')?.remove(); byId('po-prefixes').insertAdjacentHTML('beforeend', prefixRow({ legalEntityId: '', prefix: '' })); });
        root.addEventListener('click', (e) => { const b = e.target.closest && e.target.closest('.po-row-remove'); if (b) { b.closest('.po-tier-row, .po-prefix-row')?.remove(); } });
    };

    return { init: init, problem: problem };
})();

if (typeof document !== 'undefined') { document.addEventListener('DOMContentLoaded', () => PreOrderSettingsForm.init()); }
