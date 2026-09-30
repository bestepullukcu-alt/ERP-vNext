/*
 * MOD-0280-FU01 T2b — Time Entry settings.
 *
 * The time-administrator pool is one position (MOD-0288's own list, the existing positions reader). The timer switch is
 * per legal entity and OFF for every entity that has no row (D12): the page lists EVERY legal entity of the tenant
 * (MDM lookup) merged with the stored switch rows. Switching ON needs a reason — the legal basis — written on the page
 * (never a browser prompt): an empty reason is stopped here and refused by Platform too (TIMER_SWITCH_REASON_REQUIRED).
 * Every write carries the row's expectedVersion (0 when there is no row). No inline style (FG-003).
 *
 * T3 (pack §21.3 N3) — the weekly reminder switch rides the SAME settings PUT as the pool (one versioned, audited
 * command): it sends the pool as it stands, so turning the reminder on or off never clears the pool, and the pool save
 * leaves the reminder out (the server keeps it). A refused save puts the switch back where the server has it.
 */
(function (root) {
    'use strict';

    var doc = root.document;
    var core = root.TimeEntryCore;
    var L = {};
    var state = { settings: null, switches: [], entities: [], opening: null };

    function t(key) { return L[key] || key; }
    function fmt(text) {
        var args = Array.prototype.slice.call(arguments, 1);
        return args.reduce(function (out, value, i) { return out.split('{' + i + '}').join(String(value)); }, String(text || ''));
    }
    function byId(id) { return doc.getElementById(id); }
    function el(tag, cls, text) {
        var node = doc.createElement(tag);
        if (cls) { node.className = cls; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        return node;
    }
    function clear(node) { while (node && node.firstChild) { node.removeChild(node.firstChild); } return node; }
    function announce(message, kind) {
        var line = byId('tsNotice');
        if (!line) { return; }
        line.textContent = message || '';
        line.className = 'time-entry-notice time-entry-notice-' + (kind || 'info');
    }
    function failure(result) {
        return core.failureMessage({ status: result.status, reasonCode: result.reasonCode }, t);
    }
    function instant(iso) {
        if (!iso) { return t('NeverChanged'); }
        try { return new Intl.DateTimeFormat(doc.documentElement.getAttribute('lang') || 'en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(iso)); } catch (e) { return iso; }
    }

    function readL10n() {
        var read = function (id) {
            var node = byId(id);
            try { return node ? JSON.parse(node.textContent || '{}') : {}; } catch (e) { return {}; }
        };
        return Object.assign({}, read('time-entry-errors-l10n'), read('timesettings-l10n'));
    }

    function unwrapList(payload) {
        if (Array.isArray(payload)) { return payload; }
        var data = payload && (payload.data !== undefined ? payload.data : payload.Data);
        if (Array.isArray(data)) { return data; }
        return (payload && payload.items) || (data && data.items) || [];
    }

    function request(method, url, body) {
        var options = { method: method, credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
        if (body !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        return root.fetch(url, options).then(function (response) {
            return response.text().then(function (text) {
                var parsed = null;
                try { parsed = text ? JSON.parse(text) : null; } catch (e) { parsed = null; }
                return { ok: response.ok, status: response.status, body: parsed, data: parsed && parsed.data !== undefined ? parsed.data : parsed, reasonCode: parsed && (parsed.reason_code || parsed.reasonCode) || null };
            });
        }, function () { return { ok: false, status: 0, body: null, data: null, reasonCode: null }; });
    }

    // ── pool ───────────────────────────────────────────────────────────────────────────────────────────────────

    function renderPool(positions) {
        var select = byId('tsPoolPosition');
        var none = select.querySelector('option[value=""]');
        clear(select);
        select.appendChild(none || el('option', null, t('PoolNone')));
        positions.forEach(function (p) {
            var id = p.id || p.Id;
            if (!id) { return; }
            var code = p.code || p.Code || '';
            var name = p.name || p.Name || '';
            var option = el('option', null, code ? code + ' — ' + name : name);
            option.value = id;
            select.appendChild(option);
        });
        select.value = state.settings && state.settings.timeAdminPoolPositionId ? state.settings.timeAdminPoolPositionId : '';
        var $ = root.jQuery;
        if ($ && $.fn && $.fn.select2) { $(select).select2({ width: '100%' }); }
    }

    function savePool() {
        var value = byId('tsPoolPosition').value || null;
        return request('PUT', '/TimeEntry/api/settings', {
            expectedVersion: state.settings ? state.settings.version : 0, timeAdminPoolPositionId: value
        }).then(function (result) {
            if (!result.ok) { announce(failure(result), 'warning'); return result; }
            state.settings = result.data;
            announce(t('PoolSaved'));
            if (typeof root.showToast === 'function') { root.showToast(t('PoolSaved'), 'success'); }
            return result;
        });
    }

    // ── weekly reminder ─────────────────────────────────────────────────────────────────────────────────────────

    function renderReminder() {
        var box = byId('tsWeeklyReminder');
        if (!box) { return; }
        box.checked = !!(state.settings && state.settings.weeklyReminderEnabled);
        box.disabled = false;
    }

    function saveReminder(enabled) {
        var box = byId('tsWeeklyReminder');
        if (box) { box.disabled = true; }
        return request('PUT', '/TimeEntry/api/settings', {
            expectedVersion: state.settings ? state.settings.version : 0,
            timeAdminPoolPositionId: state.settings ? state.settings.timeAdminPoolPositionId || null : null,
            weeklyReminderEnabled: !!enabled
        }).then(function (result) {
            if (!result.ok) {
                announce(result.reasonCode ? failure(result) : t('ReminderSaveFailed'), 'warning');
                renderReminder();
                return result;
            }
            state.settings = result.data;
            renderReminder();
            var message = state.settings.weeklyReminderEnabled ? t('ReminderSavedOn') : t('ReminderSavedOff');
            announce(message);
            if (typeof root.showToast === 'function') { root.showToast(message, 'success'); }
            return result;
        });
    }

    // ── switches ───────────────────────────────────────────────────────────────────────────────────────────────

    /** Every legal entity of the tenant with its switch — a missing row is OFF (D12). Rows for entities the lookup did
     * not return are still shown, so a switched-on entity can always be switched off. */
    function mergedRows() {
        var byEntity = {};
        state.switches.forEach(function (s) { byEntity[String(s.legalEntityId)] = s; });
        var rows = state.entities.map(function (e) {
            var id = String(e.legalEntityId || e.id);
            return { legalEntityId: id, name: e.displayName || e.legalName || e.code || id, code: e.code || '', setting: byEntity[id] || null };
        });
        state.switches.forEach(function (s) {
            if (!rows.some(function (r) { return r.legalEntityId === String(s.legalEntityId); })) {
                rows.push({ legalEntityId: String(s.legalEntityId), name: String(s.legalEntityId), code: '', setting: s });
            }
        });
        return rows;
    }

    function renderSwitches() {
        var host = clear(byId('tsSwitches'));
        byId('tsLoading').hidden = true;
        var rows = mergedRows();
        if (!rows.length) {
            host.appendChild(el('p', 'text-muted', t('NoLegalEntities')));
            return;
        }
        var table = el('table', 'table time-entry-switches');
        var head = el('tr');
        [t('ColLegalEntity'), t('ColTimer'), t('ColChanged'), t('ColReason'), ''].forEach(function (h) { head.appendChild(el('th', null, h)); });
        var thead = el('thead'); thead.appendChild(head); table.appendChild(thead);
        var body = el('tbody');
        rows.forEach(function (row) {
            var on = !!(row.setting && row.setting.timerEnabled);
            var tr = el('tr', 'time-entry-switch-row');
            tr.setAttribute('data-legal-entity', row.legalEntityId);
            var name = el('td');
            name.appendChild(el('span', 'fw-medium', row.name));
            if (row.code) { name.appendChild(el('small', 'text-muted d-block', row.code)); }
            tr.appendChild(name);
            var status = el('td');
            status.appendChild(el('span', 'badge ' + (on ? 'bg-label-success' : 'bg-label-secondary'), on ? t('TimerOn') : t('TimerOff')));
            tr.appendChild(status);
            tr.appendChild(el('td', 'text-muted', row.setting ? instant(row.setting.changedAtUtc) : t('NeverChanged')));
            tr.appendChild(el('td', 'time-entry-switch-reason', row.setting && row.setting.reason ? row.setting.reason : '—'));
            var action = el('td', 'text-end');
            var button = el('button', 'btn btn-sm ' + (on ? 'btn-label-danger' : 'btn-label-primary'), on ? t('SwitchOff') : t('SwitchOn'));
            button.type = 'button';
            button.setAttribute('data-switch', on ? 'off' : 'on');
            button.addEventListener('click', function () {
                if (on) { switchOff(row); } else { state.opening = row.legalEntityId; renderSwitches(); }
            });
            action.appendChild(button);
            tr.appendChild(action);
            body.appendChild(tr);

            if (!on && state.opening === row.legalEntityId) {
                var formRow = el('tr', 'time-entry-switch-form');
                var cell = el('td');
                cell.colSpan = 5;
                var label = el('label', 'form-label', t('SwitchReason'));
                label.setAttribute('for', 'tsReason-' + row.legalEntityId);
                var field = el('textarea', 'form-control');
                field.id = 'tsReason-' + row.legalEntityId;
                field.rows = 2; field.maxLength = 1000;
                var error = el('div', 'invalid-feedback time-entry-inline-error');
                error.id = 'tsReasonError'; error.hidden = true;
                var actions = el('div', 'time-entry-correction-actions');
                var save = el('button', 'btn btn-sm btn-primary', t('SwitchSave'));
                save.type = 'button'; save.id = 'tsSwitchSave';
                save.addEventListener('click', function () { switchOn(row, field.value); });
                var cancel = el('button', 'btn btn-sm btn-label-secondary', t('Cancel'));
                cancel.type = 'button';
                cancel.addEventListener('click', function () { state.opening = null; renderSwitches(); });
                actions.appendChild(save); actions.appendChild(cancel);
                cell.appendChild(label); cell.appendChild(field); cell.appendChild(error); cell.appendChild(actions);
                formRow.appendChild(cell);
                body.appendChild(formRow);
            }
        });
        table.appendChild(body);
        var wrap = el('div', 'table-responsive');
        wrap.appendChild(table);
        host.appendChild(wrap);
    }

    function putSwitch(row, enabled, reason) {
        return request('PUT', '/TimeEntry/api/settings/legal-entities/' + encodeURIComponent(row.legalEntityId), {
            expectedVersion: row.setting ? row.setting.version : 0, timerEnabled: enabled, reason: reason
        }).then(function (result) {
            if (!result.ok) { announce(failure(result), 'warning'); return result; }
            var saved = result.data;
            state.switches = state.switches.filter(function (s) { return String(s.legalEntityId) !== row.legalEntityId; }).concat([saved]);
            state.opening = null;
            var message = fmt(enabled ? t('SwitchSavedOn') : t('SwitchSavedOff'), row.name);
            announce(message);
            if (typeof root.showToast === 'function') { root.showToast(message, 'success'); }
            renderSwitches();
            return result;
        });
    }

    /** ON needs its reason — stopped here before anything is sent. */
    function switchOn(row, reason) {
        var text = String(reason || '').trim();
        if (!text) {
            var error = byId('tsReasonError');
            if (error) { error.textContent = t('SwitchReasonRequired'); error.hidden = false; }
            return Promise.resolve({ ok: false });
        }
        return putSwitch(row, true, text);
    }

    function switchOff(row) {
        var go = function () { putSwitch(row, false, null); };
        if (typeof root.showConfirm === 'function') { root.showConfirm(fmt(t('SwitchOffConfirm'), row.name), go); } else { go(); }
    }

    // ── boot ───────────────────────────────────────────────────────────────────────────────────────────────────

    function init() {
        if (!byId('timeEntrySettings')) { return Promise.resolve(null); }
        L = readL10n();
        byId('tsSavePool').addEventListener('click', savePool);
        var reminder = byId('tsWeeklyReminder');
        if (reminder) { reminder.addEventListener('change', function () { saveReminder(reminder.checked); }); }
        return Promise.all([
            request('GET', '/TimeEntry/api/settings'),
            request('GET', '/TimeEntry/Settings/lookup/positions'),
            request('GET', '/TimeEntry/api/settings/legal-entities'),
            request('GET', '/TimeEntry/Settings/lookup/legal-entities')
        ]).then(function (results) {
            state.settings = results[0].ok ? results[0].data : { timeAdminPoolPositionId: null, version: 0, weeklyReminderEnabled: false };
            renderReminder();
            if (!results[1].ok) { announce(t('PositionsUnavailable'), 'warning'); }
            renderPool(results[1].ok ? unwrapList(results[1].body) : []);
            state.switches = results[2].ok ? unwrapList(results[2].body) : [];
            if (!results[3].ok) { announce(t('LegalEntitiesUnavailable'), 'warning'); }
            state.entities = results[3].ok ? unwrapList(results[3].body) : [];
            renderSwitches();
            return state;
        });
    }

    root.TimeEntrySettings = { init: init, switchOn: switchOn, switchOff: switchOff, savePool: savePool, saveReminder: saveReminder, rows: mergedRows, state: function () { return state; } };

    if (doc.readyState === 'loading') { doc.addEventListener('DOMContentLoaded', init); }
    else if (!root.__timeSettingsNoAutoInit) { init(); }
})(typeof window !== 'undefined' ? window : globalThis);
