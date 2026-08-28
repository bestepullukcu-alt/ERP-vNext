/*
 * MOD-0151 FU08 — Territory Import / Export screen.
 *
 * Flow contract (mirrors the backend): a file can only be applied after a dry-run has returned canApply=true for
 * THAT file. Changing the selected file invalidates the previous preview, so the Apply button can never act on a
 * plan the operator did not see.
 */
(function () {
    'use strict';

    var ctx = window.TerritoryImportExport;
    if (!ctx) { return; }

    var fileInput = document.getElementById('tm-import-file');
    var strictInput = document.getElementById('tm-import-strict');
    var dryRunButton = document.getElementById('tm-import-dryrun');
    var applyButton = document.getElementById('tm-import-apply');
    var resultCard = document.getElementById('tm-import-result');
    var resultTitle = document.getElementById('tm-import-result-title');
    var counters = document.getElementById('tm-import-counters');
    var blockedBox = document.getElementById('tm-import-blocked');
    var fileMessages = document.getElementById('tm-import-file-messages');
    var sheetBody = document.querySelector('#tm-import-sheets tbody');
    var rowBody = document.querySelector('#tm-import-rows tbody');
    var onlyBlocking = document.getElementById('tm-import-only-blocking');
    var runBody = document.querySelector('#tm-import-runs tbody');

    // The preview the Apply button is allowed to act on: name+size+lastModified of the previewed file.
    var approvedFileKey = null;
    var lastRows = [];

    function fileKey(file) {
        return file ? file.name + '|' + file.size + '|' + file.lastModified : null;
    }

    function token() {
        var el = document.querySelector('#tm-import-form input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function esc(value) {
        if (value === null || value === undefined) { return ''; }
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function toast(message, isError) {
        if (window.showToast) { window.showToast(message, isError ? 'error' : 'success'); return; }
        if (isError) { console.error(message); } else { console.log(message); }
    }

    function severityBadge(row) {
        var cls = row.blocking ? 'bg-label-danger'
            : row.severity === 'warning' ? 'bg-label-warning' : 'bg-label-secondary';
        return '<span class="badge ' + cls + '">' + esc(row.severity) + '</span>';
    }

    function statusBadge(status) {
        var map = {
            create: 'bg-label-success', update: 'bg-label-info', end: 'bg-label-warning',
            no_change: 'bg-label-secondary', skip: 'bg-label-secondary',
            error: 'bg-label-danger', conflict: 'bg-label-danger',
            not_applied: 'bg-label-warning', applied: 'bg-label-success'
        };
        return '<span class="badge ' + (map[status] || 'bg-label-secondary') + '">' + esc(status) + '</span>';
    }

    function renderCounters(summary, preview) {
        var items = [
            [ctx.l10n.creates, summary.creates, 'bg-label-success'],
            [ctx.l10n.updates, summary.updates, 'bg-label-info'],
            [ctx.l10n.ends, summary.ends, 'bg-label-warning'],
            [ctx.l10n.skips, summary.skips, 'bg-label-secondary'],
            [ctx.l10n.errors, summary.errors, 'bg-label-danger'],
            [ctx.l10n.conflicts, summary.conflicts, 'bg-label-danger'],
            [ctx.l10n.warnings, summary.warnings, 'bg-label-warning']
        ];
        var html = items.map(function (item) {
            return '<span class="badge ' + item[2] + '">' + esc(item[0]) + ': ' + esc(item[1]) + '</span>';
        }).join(' ');

        if (preview.previousAppliesOfThisFile > 0) {
            html += ' <span class="badge bg-label-secondary">' + esc(ctx.l10n.reRun) + ': '
                + esc(preview.previousAppliesOfThisFile) + '</span>';
        }
        counters.innerHTML = html;
    }

    function renderSheets(sheets) {
        if (!sheets || !sheets.length) { sheetBody.innerHTML = ''; return; }
        sheetBody.innerHTML = sheets.map(function (s) {
            var outcome = s.applied
                ? '<span class="badge bg-label-success">' + esc(ctx.l10n.applied) + '</span>'
                : '<span class="badge bg-label-secondary">' + esc(ctx.l10n.notApplied) + '</span>'
                    + (s.notAppliedReason ? ' <span class="text-muted small">' + esc(s.notAppliedReason) + '</span>' : '');
            return '<tr>'
                + '<td>' + esc(s.sheet) + '</td>'
                + '<td class="text-end">' + esc(s.totalRows) + '</td>'
                + '<td class="text-end">' + (s.blockingRows > 0
                    ? '<span class="text-danger fw-semibold">' + esc(s.blockingRows) + '</span>'
                    : '0') + '</td>'
                + '<td class="text-end">' + esc(s.created) + '</td>'
                + '<td class="text-end">' + esc(s.updated) + '</td>'
                + '<td class="text-end">' + esc(s.ended) + '</td>'
                + '<td class="text-end">' + esc(s.skipped) + '</td>'
                + '<td>' + outcome + '</td>'
                + '</tr>';
        }).join('');
    }

    function renderRows() {
        var rows = onlyBlocking && onlyBlocking.checked
            ? lastRows.filter(function (r) { return r.blocking; })
            : lastRows;

        rowBody.innerHTML = rows.map(function (r) {
            return '<tr class="' + (r.blocking ? 'table-danger' : '') + '">'
                + '<td>' + esc(r.sheet) + '</td>'
                + '<td class="text-end">' + esc(r.rowNumber) + '</td>'
                + '<td>' + severityBadge(r) + '</td>'
                + '<td>' + esc(r.operation || '') + '</td>'
                + '<td>' + esc(r.resolvedKey || '') + '</td>'
                + '<td>' + statusBadge(r.status) + '</td>'
                + '<td>' + esc(r.message)
                + (r.errorCode ? ' <span class="text-muted small">(' + esc(r.errorCode) + ')</span>' : '')
                + (r.changedFields && r.changedFields.length
                    ? '<div class="text-muted small">' + esc(r.changedFields.join(', ')) + '</div>' : '')
                + '</td>'
                + '<td class="text-muted small">' + esc(r.suggestedFix || '') + '</td>'
                + '</tr>';
        }).join('');
    }

    function renderPreview(preview, wasApply) {
        resultCard.classList.remove('d-none');
        resultTitle.textContent = wasApply ? ctx.l10n.applyResult : ctx.l10n.dryRunResult;

        renderCounters(preview.summary || {}, preview);
        renderSheets(preview.sheets);
        lastRows = preview.rows || [];
        renderRows();

        var messages = [];
        (preview.fileErrors || []).forEach(function (e) {
            messages.push('<div class="alert alert-danger py-2 mb-2">' + esc(e) + '</div>');
        });
        (preview.fileWarnings || []).forEach(function (w) {
            messages.push('<div class="alert alert-warning py-2 mb-2">' + esc(w) + '</div>');
        });
        fileMessages.innerHTML = messages.join('');

        if (preview.blockedReason) {
            blockedBox.classList.remove('d-none');
            blockedBox.textContent = preview.blockedReason;
        } else {
            blockedBox.classList.add('d-none');
            blockedBox.textContent = '';
        }
    }

    function setBusy(busy) {
        dryRunButton.disabled = busy;
        applyButton.disabled = busy || !approvedFileKey;
    }

    function send(url, wasApply) {
        var file = fileInput.files && fileInput.files[0];
        if (!file) { toast(ctx.l10n.selectFile, true); return; }
        if (!/\.xlsx$/i.test(file.name)) { toast(ctx.l10n.xlsxOnly, true); return; }

        var form = new FormData();
        form.append('file', file);
        form.append('strictMode', strictInput.checked ? 'true' : 'false');
        form.append('__RequestVerificationToken', token());

        setBusy(true);
        fetch(url, { method: 'POST', body: form, credentials: 'same-origin' })
            .then(function (res) { return res.json(); })
            .then(function (payload) {
                if (!payload || !payload.success || !payload.data) {
                    var errors = (payload && payload.errors) || [ctx.l10n.gatewayError];
                    toast(errors.join(' '), true);
                    return;
                }

                var preview = payload.data;
                renderPreview(preview, wasApply);

                if (wasApply) {
                    // A file may be applied once per preview: force a fresh dry-run before another write.
                    approvedFileKey = null;
                    toast(preview.applied ? ctx.l10n.applied : (preview.blockedReason || ctx.l10n.applyBlocked), !preview.applied);
                    loadRuns();
                } else {
                    approvedFileKey = preview.canApply ? fileKey(file) : null;
                    if (!preview.canApply) {
                        toast(preview.blockedReason || ctx.l10n.nothingToApply, true);
                    }
                }
            })
            .catch(function () { toast(ctx.l10n.gatewayError, true); })
            .finally(function () { setBusy(false); });
    }

    function loadRuns() {
        fetch(ctx.urls.runs, { credentials: 'same-origin' })
            .then(function (res) { return res.json(); })
            .then(function (payload) {
                var items = (payload && payload.success && payload.data && payload.data.items) || [];
                if (!items.length) {
                    runBody.innerHTML = '<tr><td colspan="9" class="text-center text-muted">'
                        + esc(ctx.l10n.noRuns) + '</td></tr>';
                    return;
                }

                runBody.innerHTML = items.map(function (run) {
                    var statusClass = run.status === 'applied' ? 'bg-label-success'
                        : run.status === 'partially-applied' ? 'bg-label-warning' : 'bg-label-danger';
                    return '<tr>'
                        + '<td>' + esc(new Date(run.uploadedAt).toLocaleString()) + '</td>'
                        + '<td>' + esc(run.fileName) + '</td>'
                        + '<td>' + esc(run.uploadedBy) + '</td>'
                        + '<td><span class="badge ' + statusClass + '">' + esc(run.status) + '</span></td>'
                        + '<td class="text-end">' + esc(run.creates) + '</td>'
                        + '<td class="text-end">' + esc(run.updates) + '</td>'
                        + '<td class="text-end">' + esc(run.ends) + '</td>'
                        + '<td class="text-end">' + esc(run.errorCount) + '</td>'
                        + '<td class="text-muted small">' + esc((run.sheetOutcomes || []).join(' · ')) + '</td>'
                        + '</tr>';
                }).join('');
            })
            .catch(function () {
                runBody.innerHTML = '<tr><td colspan="9" class="text-center text-muted">'
                    + esc(ctx.l10n.gatewayError) + '</td></tr>';
            });
    }

    fileInput.addEventListener('change', function () {
        // A new file invalidates the approved plan — Apply must never act on an unreviewed file.
        approvedFileKey = null;
        applyButton.disabled = true;
    });

    strictInput.addEventListener('change', function () {
        approvedFileKey = null;
        applyButton.disabled = true;
    });

    if (onlyBlocking) { onlyBlocking.addEventListener('change', renderRows); }

    dryRunButton.addEventListener('click', function () { send(ctx.urls.dryRun, false); });

    applyButton.addEventListener('click', function () {
        var file = fileInput.files && fileInput.files[0];
        if (!approvedFileKey || approvedFileKey !== fileKey(file)) {
            toast(ctx.l10n.applyBlocked, true);
            return;
        }

        if (window.showConfirm) {
            window.showConfirm({ title: ctx.l10n.confirmApply, icon: 'bx-check-double' }, function () {
                send(ctx.urls.apply, true);
            });
            return;
        }

        if (window.confirm(ctx.l10n.confirmApply)) { send(ctx.urls.apply, true); }
    });

    if (!ctx.canApply) { applyButton.classList.add('d-none'); }

    loadRuns();
})();
