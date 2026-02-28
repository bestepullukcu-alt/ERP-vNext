// --- CONFIG ---
const port2 = protocol === 'https:' ? '5060' : '5053';
const BASE_URL = `${window.ApiBaseUrl}`;

const PAGE_SIZE_DROPDOWN = document.getElementById('pageSize');
const SEARCH_INPUT = document.getElementById('searchUser');
const T_BODY = document.getElementById('documentsContainer');
const PAGE_INFO = document.getElementById('file-page-info');
const BTN_PREV = document.getElementById('file-prev-btn');
const BTN_NEXT = document.getElementById('file-next-btn');

let allData = [], filtered = [];
let pageSize = Number(PAGE_SIZE_DROPDOWN?.value || 10);
let pageIndex = 0;
let deleteId = null;

// --- INIT ---
document.addEventListener('DOMContentLoaded', async () => {
    document.getElementById('btnAddAgreement')?.setAttribute('href', '/Agreement/_CreateAgreement');

    bindListUI();
    bindDeleteFlow();

    await loadAgreements();
});

// --- API ---
async function loadAgreements() {
    try {
        const userId = window.getUserId?.();
        const res = await fetch(`${BASE_URL}/services/PvOrganization/Agreement/GetAgreements?userId=${encodeURIComponent(userId)}`);
        if (!res.ok) throw new Error('HTTP ' + res.status);
        const json = await res.json();

        allData = Array.isArray(json?.data) ? json.data : [];
        filter('');
        render();
    } catch {
        allData = []; filter(''); render();
        showToast('Agreements yüklenemedi.', 'error');
    }
}

// --- UI BINDINGS ---
function bindListUI() {
    PAGE_SIZE_DROPDOWN?.addEventListener('change', () => {
        pageSize = Number(PAGE_SIZE_DROPDOWN.value) || 10;
        pageIndex = 0; render();
    });
    BTN_PREV?.addEventListener('click', () => changePage(-1));
    BTN_NEXT?.addEventListener('click', () => changePage(1));
    if (SEARCH_INPUT) {
        const deb = debounce(() => { filter((SEARCH_INPUT.value || '').trim().toLowerCase()); pageIndex = 0; render(); }, 250);
        SEARCH_INPUT.addEventListener('input', deb);
    }
}

// --- DELETE FLOW (uses your modal as-is) ---
const DELETE_URL = `${BASE_URL}/services/PvOrganization/Agreement/DeleteAgreement`;

function bindDeleteFlow() {
    document.addEventListener('click', async (e) => {
        // open modal
        const delBtn = e.target.closest('.delete-record');
        if (delBtn) {
            deleteId = delBtn.getAttribute('data-id') || null;
            if (!deleteId) return showToast('Kayıt id bulunamadı.', 'error');
            new bootstrap.Modal(document.getElementById('deleteConfirmModal')).show();
            return;
        }

        // confirm click
        if (e.target.closest('#confirmDeleteBtn')) {
            e.preventDefault();
            if (!deleteId) return;

            try {
                const res = await fetch(DELETE_URL, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ id: deleteId, modifiedBy: window.getUserName?.() })
                });
                const json = await res.json();
                if (!res.ok || json?.data !== true) throw new Error(json?.errors?.[0]?.message || 'Delete failed');

                const idStr = String(deleteId);
                allData = allData.filter(x => String(x.id) !== idStr);
                filtered = filtered.filter(x => String(x.id) !== idStr);
                render();

                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal'))?.hide();
                showToast('Agreement deleted.');
            } catch (err) {
                showToast(err.message || 'Delete failed.', 'error');
            } finally {
                deleteId = null;
            }
        }
    });
}

// --- RENDER ---
function render() {
    const start = pageIndex * pageSize;
    const end = Math.min(start + pageSize, filtered.length);
    const rows = filtered.slice(start, end).map(renderRow).join('');
    T_BODY.innerHTML = rows || `<tr><td colspan="11" class="text-center text-muted">No data</td></tr>`;

    PAGE_INFO.textContent = `Showing ${filtered.length ? start + 1 : 0} to ${filtered.length ? end : 0} of ${filtered.length} entries`;
    BTN_PREV.disabled = pageIndex === 0;
    BTN_NEXT.disabled = end >= filtered.length;
}

function renderRow(x) {
    const id = safe(x.id);                    
    const agr = safe(x.agreementNumber) || '-';
    const type = safe(x.agreementType?.name) || '-';
    const act = x.status === '1';
    const status = act ? `<span class="badge bg-success">Active</span>` : `<span class="badge bg-secondary">Passive</span>`;

    return `
    <tr data-id="${id || ''}">
      <td style="width:28px;"></td>
      <td>${agr}</td>
      <td><span class="badge bg-label-primary">${type}</span></td>
      <td>${status}</td>
      <td>${renderParties(x.parties)}</td>
      <td>${fmtDate(x.startDate)}</td>
      <td>${fmtDate(x.endDate)}</td>
      <td>${fmtMoney(x.agreementValue)}</td>
      <td>${safe(x.country) || '-'}</td>
      <td>${fmtDate(x.modifiedDate)}</td>
      <td class="d-flex gap-2 align-items-center">
        <button class="btn btn-icon preview-record" data-id="${id || ''}"><i class='bx bx-show'></i></button>
        <button class="btn btn-icon edit-record"    data-id="${id || ''}"><i class='bx bx-edit'></i></button>
        <button class="btn btn-icon delete-record"  data-id="${id || ''}" ${id ? '' : 'disabled title="Missing id"'}><i class='bx bx-trash'></i></button>
      </td>
    </tr>`;
}

function renderParties(p) {
    if (!Array.isArray(p) || !p.length) return '-';
    const main = safe(p[0]?.name) || '-';
    const subs = p.slice(1).map(i => `&ndash; ${safe(i?.name)}`).join('<br/>');
    return `<div><div>${main}</div>${subs ? `<small class="text-muted">${subs}</small>` : ''}</div>`;
}

// --- SEARCH & PAGING ---
function filter(q) {
    if (!q) { filtered = [...allData]; return; }
    filtered = allData.filter(x => (
        `${safe(x.agreementNumber)} ${safe(x.agreementType?.name)} ${safe(x.country)} ${fmtDate(x.startDate)} ${fmtDate(x.endDate)} ${(x.parties || []).map(p => safe(p?.name)).join(' ')}`
    ).toLowerCase().includes(q));
}
function changePage(d) {
    const total = Math.max(1, Math.ceil(filtered.length / pageSize));
    pageIndex = Math.min(Math.max(pageIndex + d, 0), total - 1);
    render();
}

// --- UTILS ---
function fmtDate(v) { if (!v) return '-'; const d = new Date(v); return isNaN(d) ? '-' : `${String(d.getDate()).padStart(2, '0')}.${String(d.getMonth() + 1).padStart(2, '0')}.${d.getFullYear()}`; }
function fmtMoney(v) { if (v == null) return '-'; try { return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(Number(v)); } catch { return String(v); } }
function safe(v) { return v == null ? '' : String(v); }
function debounce(fn, ms = 300) { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; }

// --- TOAST ---
function showToast(message, type = "success") {
    const el = document.getElementById("appToast");
    const body = el?.querySelector(".toast-body");
    const head = el?.querySelector("#appToastHeader");
    if (!el || !body || !head) return;
    body.innerHTML = message;
    el.classList.remove("bg-success", "bg-danger", "bg-warning", "bg-info");
    const m = { success: ["bg-success", "Successful"], error: ["bg-danger", "Error"], warning: ["bg-warning", "Warning"], info: ["bg-info", "Info"] }[type] || ["bg-info", "Info"];
    el.classList.add(m[0]); head.textContent = m[1];
    bootstrap.Toast.getOrCreateInstance(el).show();
}
