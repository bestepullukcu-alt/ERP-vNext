/*
 * WP-EMAIL-LANG-01 delivery 2 — My profile.
 *
 * Reads the signed-in user's own record (GET /Profile/api/me) and saves ONE thing: the language their mail is written in
 * (PUT /Profile/api/preferences, body { preferredLanguage }). The web tier rebuilds that body and, on success, writes the
 * screen-language cookie too (CT decision 1 = A), so a saved language reloads the page in it. Clearing the choice keeps
 * the screen as it is. A refusal is said by its code in the screen's language. No inline style (FG-003).
 */
(function (root) {
    'use strict';

    var doc = root.document;
    var L = {};
    var current = null;
    var SAVED_FLAG = 'profile.languageSaved';

    function byId(id) { return doc.getElementById(id); }
    function t(key) { return L[key] || key; }

    function readL10n() {
        var node = byId('profile-l10n');
        try { L = node ? JSON.parse(node.textContent || '{}') : {}; } catch (e) { L = {}; }
    }

    function notify(message, kind) {
        var line = byId('profileNotice');
        if (!line) { return; }
        line.textContent = message || '';
        line.className = 'alert alert-' + (kind || 'info') + (message ? '' : ' d-none');
    }

    function show(loaded) {
        byId('profileSkeleton').classList.toggle('d-none', loaded);
        byId('profileCard').classList.toggle('d-none', !loaded);
    }

    function messageFor(body) {
        var codes = (L.ErrorCodes || {});
        var listed = (body && (body.errorCodes || body.errors)) || [];
        for (var i = 0; i < listed.length; i++) {
            var entry = listed[i];
            var code = typeof entry === 'string' ? entry : (entry && (entry.code || entry.Code));
            if (code && codes[code]) { return t(codes[code]); }
        }
        if (body && body.reason_code && codes[body.reason_code]) { return t(codes[body.reason_code]); }
        return t('SaveFailed');
    }

    function request(method, path, payload) {
        var options = { method: method, credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
        if (payload !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(payload);
        }
        return root.fetch(path, options).then(function (response) {
            return response.text().then(function (text) {
                var body = null;
                try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
                return { ok: response.ok, status: response.status, body: body };
            });
        });
    }

    function load() {
        return request('GET', '/Profile/api/me').then(function (result) {
            var data = result.ok && result.body ? result.body.data : null;
            if (!data) {
                show(false);
                byId('profileSkeleton').classList.add('d-none');
                notify(t('LoadFailed'), 'danger');
                return;
            }
            byId('profileName').textContent = [data.firstName, data.lastName].filter(Boolean).join(' ');
            byId('profileEmail').textContent = data.email || '';
            current = data.preferredLanguage || '';
            byId('profileLanguage').value = current;
            show(true);
            var savedBeforeReload = false;
            try {
                savedBeforeReload = root.sessionStorage.getItem(SAVED_FLAG) === '1';
                root.sessionStorage.removeItem(SAVED_FLAG);
            } catch (e) { savedBeforeReload = false; }
            if (savedBeforeReload) { notify(t('Saved'), 'success'); }
        }).catch(function () {
            byId('profileSkeleton').classList.add('d-none');
            notify(t('LoadFailed'), 'danger');
        });
    }

    function save() {
        var button = byId('profileSave');
        var chosen = byId('profileLanguage').value || null;
        button.disabled = true;
        notify('');
        return request('PUT', '/Profile/api/preferences', { preferredLanguage: chosen }).then(function (result) {
            button.disabled = false;
            if (!result.ok) {
                notify(messageFor(result.body), 'danger');
                return;
            }
            var saved = result.body && result.body.data ? result.body.data.preferredLanguage : chosen;
            if (saved && saved !== current) {
                // The web tier set the screen-language cookie: draw the page again in the chosen language, and say so there.
                try { root.sessionStorage.setItem(SAVED_FLAG, '1'); } catch (e) { /* the notice is a nicety */ }
                root.location.reload();
                return;
            }
            current = saved || '';
            notify(saved ? t('Saved') : t('Cleared'), 'success');
        }).catch(function () {
            button.disabled = false;
            notify(t('SaveFailed'), 'danger');
        });
    }

    function start() {
        readL10n();
        if (!byId('profilePage')) { return; }
        byId('profileSave').addEventListener('click', save);
        load();
    }

    if (doc.readyState === 'loading') {
        doc.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }

    root.ProfilePage = { messageFor: messageFor };
})(window);
