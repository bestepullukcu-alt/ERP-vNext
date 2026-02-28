const port2 = protocol === 'https:' ? '5055' : '5050';

/* -------------------- State --------------------- */
let folders = [];
let documents = [];
let currentFolderId = null;

// paging
let folderPage = 1, folderPageSize = 8;
let filePage = 1, filePageSize = 10;

// delete buffers
let folderIdToDelete = null;
let fileIdToDelete = null;

// move buffer
let selectedFolderId = null;

// once-guards
let __wiredDelegated = false;
let __wiredDeleteFolder = false;
let __wiredDeleteFile = false;
let __wiredShareInit = false;

// version history deps
let vhDeps = null;

document.addEventListener('DOMContentLoaded', async () => {
    try {
        const urlParams = new URLSearchParams(window.location.search);
        const folderId = urlParams.get('id');
        if (!folderId) return;
        currentFolderId = folderId;

        await renderBreadcrumb(folderId);

        const uploadBtn = document.getElementById('uploadDocumentBtn');
        if (uploadBtn && currentFolderId) uploadBtn.href = `/DocumentationSystem/_UploadDocument?id=${currentFolderId}`;

        initCreateFolderVisibility();

        const createFolderForm = document.getElementById('createFolderForm');
        if (createFolderForm) createFolderForm.addEventListener('submit', handleCreateFolderSubmit);

        bindFolderPaging();
        bindFilePaging();

        initVersionHistoryDeps();

        await loadFolders();
        await loadDocuments();

        bindDeleteFolderEvent();
        bindDeleteDocumentEvent();
        bindEditFolderEvent();
    } catch (err) {
        console.error(err);
        showToast('Sayfa yüklenirken bir hata oluştu.', 'error');
    }
});

async function fetchJSON(url, options = {}, failMsg = 'İstek başarısız.') {
    try {
        const res = await fetch(url, options);
        if (!res.ok) {
            showToast(`${failMsg} (HTTP ${res.status})`, 'error');
            throw new Error(`HTTP ${res.status}`);
        }
        const json = await res.json();
        if (json && json.errors) {
            showToast(failMsg, 'error');
            throw new Error('API errors: ' + JSON.stringify(json.errors));
        }
        return json;
    } catch (e) {
        console.error('fetchJSON error:', e);
        if (!options.__silent) showToast(failMsg, 'error');
        throw e;
    }
}
function formatBytes(bytes) {
    const b = Number(bytes) || 0;
    if (b <= 0) return '-';
    const k = 1024, sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(b) / Math.log(k));
    return (b / Math.pow(k, i)).toFixed(1) + ' ' + sizes[i];
}
function formatDateWithUser(dateStr, user) {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    const main = `${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
    return `${main}<br><small class="text-muted">by ${user || '-'}</small>`;
}
function formatDate(d) {
    if (!d) return '-';
    return new Date(d).toLocaleString('tr-TR');
}
function getExtFromUrl(url) {
    try {
        const u = new URL(url);
        const p = u.pathname;
        const dot = p.lastIndexOf('.');
        return dot > -1 ? p.slice(dot) : '';
    } catch {
        const dot = url.lastIndexOf('.');
        return dot > -1 ? url.slice(dot) : '';
    }
}
function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    const toastBody = toastEl?.querySelector('.toast-body');
    const toastHeader = toastEl?.querySelector('#appToastHeader');
    if (!toastEl || !toastBody || !toastHeader) {
        console.warn('Toast elementleri bulunamadı.');
        alert(message);
        return;
    }
    toastBody.innerHTML = message;
    toastEl.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');
    switch (type) {
        case 'success': toastEl.classList.add('bg-success'); toastHeader.textContent = 'Successfull'; break;
        case 'error': toastEl.classList.add('bg-danger'); toastHeader.textContent = 'Error'; break;
        case 'warning': toastEl.classList.add('bg-warning'); toastHeader.textContent = 'Warning'; break;
        case 'info': toastEl.classList.add('bg-info'); toastHeader.textContent = 'Information'; break;
    }
    bootstrap.Toast.getOrCreateInstance(toastEl).show();
}

const UserDirectory = {
    _cache: null,
    _promise: null,

    async load(force = false) {
        if (this._cache && !force) return this._cache;
        if (this._promise && !force) return this._promise;

        this._promise = fetchJSON(
            `${protocol}//${domain}:${port2}/api/PvUser/User/GetUsersByTenantId`,
            {},
            'Kullanıcılar alınamadı.'
        ).then(json => {
            const arr = json?.data || [];
            const seen = new Set(), uniq = [];
            for (const u of arr) {
                const id = String(u.id);
                if (!seen.has(id)) { seen.add(id); uniq.push({ id, fullName: u.fullName }); }
            }
            this._cache = uniq;
            return uniq;
        }).finally(() => { this._promise = null; });

        return this._promise;
    },

    async populate(selectId, { selected = [], forceReload = false } = {}) {
        // Birden fazla aynı ID varsa: yalnız ilkini kullan
        const $all = $('#' + selectId);
        if ($all.length === 0) return;
        if ($all.length > 1) {
            console.warn(`[populate] duplicate #${selectId} = ${$all.length}`);
            // diğer kopyalardaki bootstrap-select wrapper’larını temizle
            $all.slice(1).each(function () {
                const $sel = $(this);
                if ($sel.data('selectpicker')) $sel.selectpicker('destroy');
                $sel.next('.bootstrap-select').remove();
            });
        }

        const el = document.getElementById(selectId);
        const $sel = $(el);
        if (!el) return;

        // --- GENERATION TOKEN (race fix) ---
        const gen = (Number(el.dataset.popGen || 0) + 1);
        el.dataset.popGen = String(gen);

        // Eski plugin varsa sök + wrapper'ı kaldır
        if ($sel.data('selectpicker')) $sel.selectpicker('destroy');
        $sel.next('.bootstrap-select').remove();

        // DOM tamamen boş
        el.innerHTML = '';

        // Veri al
        const users = await this.load(forceReload);

        // Eğer bu arada başka populate çağrısı başladıysa: vazgeç
        if (el.dataset.popGen !== String(gen)) return;

        // Options ekle (tekilleştir)
        const frag = document.createDocumentFragment();
        const seen = new Set();
        for (const u of users) {
            if (seen.has(u.id)) continue;
            seen.add(u.id);
            frag.appendChild(new Option(u.fullName, u.id));
        }
        el.appendChild(frag);

        // Tek sefer init + seçim + refresh
        $sel.selectpicker();
        $sel.selectpicker('val', selected && selected.length ? selected.map(String) : []);
        $sel.selectpicker('refresh');
    }
};
/* -------------------- Breadcrumb -------------------- */
async function getFolderPath(folderId) {
    const path = [];
    let currentId = folderId;
    while (currentId) {
        const json = await fetchJSON(
            `${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/GetFolderById?id=${currentId}`,
            {}, 'Klasör yolu alınamadı.'
        );
        if (!json?.data) break;
        path.unshift({ id: json.data.id, name: json.data.name });
        currentId = json.data.parentId || null;
    }
    return path;
}
async function renderBreadcrumb(folderId) {
    const bc = document.querySelector('.breadcrumb');
    if (!bc) return;
    const folderPath = await getFolderPath(folderId);
    folderPath.forEach((f, i) => {
        const last = i === folderPath.length - 1;
        bc.innerHTML += last
            ? `<li class="breadcrumb-item active" aria-current="page">${f.name}</li>`
            : `<li class="breadcrumb-item"><a href="/DocumentationSystem/_FolderDetail?id=${f.id}">${f.name}</a></li>`;
    });
}

/* -------------------- Folders ---------------------- */
async function loadFolders() {
    try {
        const json = await fetchJSON(
            `${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/GetFolderById?id=${currentFolderId}`,
            {}, 'Klasörler getirilirken bir hata oluştu.'
        );
        folders = json?.data?.childFolders || [];
        renderSubFolders(folders);
    } catch { }
}
function bindFolderPaging() {
    const prev = document.getElementById('prev-btn');
    const next = document.getElementById('next-btn');
    prev && prev.addEventListener('click', () => {
        if (folderPage > 1) { folderPage--; renderSubFolders(folders); }
    });
    next && next.addEventListener('click', () => {
        const totalPages = Math.ceil((folders?.length || 0) / folderPageSize);
        if (folderPage < totalPages) { folderPage++; renderSubFolders(folders); }
    });
}
function renderSubFolders(list) {
    const emptyView = document.getElementById('folder-empty');
    const folderList = document.getElementById('folder-list');
    const paginationControls = document.getElementById('pagination-controls');
    const pageInfo = document.getElementById('page-info');
    if (!folderList) return;
    folderList.innerHTML = '';

    if (!list || list.length === 0) {
        if (emptyView) emptyView.style.display = 'block';
        folderList.style.display = 'none';
        if (paginationControls) paginationControls.style.display = 'none';
        if (pageInfo) { pageInfo.textContent = `Page 0 of 0`; pageInfo.style.display = 'block'; }
        const prevBtn = document.getElementById('prev-btn');
        const nextBtn = document.getElementById('next-btn');
        if (prevBtn) prevBtn.disabled = true;
        if (nextBtn) nextBtn.disabled = true;
        return;
    }

    if (emptyView) emptyView.style.display = 'none';
    folderList.style.display = 'flex';
    folderList.classList.add('row');

    const startIndex = (folderPage - 1) * folderPageSize;
    const endIndex = startIndex + folderPageSize;
    const paginated = list.slice(startIndex, endIndex);

    paginated.forEach(folder => {
        let visibilityIcon = '';
        switch (folder.visibility) {
            case 'public': visibilityIcon = '<i class="fa fa-users text-warning" style="font-size: .8rem;"></i>'; break;
            case 'private': visibilityIcon = "<i class='bx bx-lock text-danger'></i>"; break;
            case 'restricted': visibilityIcon = '<i class="fa fa-users text-primary" style="font-size: .8rem;"></i>'; break;
        }
        const col = document.createElement('div');
        col.className = 'col-12 col-md-3 mb-3 position-relative';
        col.innerHTML = `
      <div class="card h-100 d-flex flex-row align-items-center position-relative folder-card p-2"
           style="cursor:pointer; border:1px solid #e0e0e0; border-radius:10px; box-shadow:0 1px 2px rgba(0,0,0,.04);">
        <div class="ps-2 pe-2 fs-4 text-primary"><i class='bx bx-folder'></i></div>
        <div class="card-body p-2">
          <h6 class="card-title fw-semibold mb-1 d-flex align-items-center gap-1">
            ${folder.name}<span class="ms-1">${visibilityIcon}</span>
          </h6>
          <p class="card-text text-muted mb-1 small">${folder.description || ''}</p>
          <small class="text-muted">${folder.documentCount || 0} documents • ${folder.subFolderCount || 0} folders</small>
        </div>
        <div class="position-absolute end-0 d-flex align-items-center" style="top:50%; transform:translateY(-50%); padding-right:10px; z-index:10;">
          <div class="dropdown">
            <i class='bx bx-dots-vertical-rounded text-muted fs-4 dropdown-toggle' data-bs-toggle="dropdown"></i>
            <ul class="dropdown-menu dropdown-menu-end shadow-sm" style="z-index:1055;">
              <li><a class="dropdown-item d-flex align-items-center gap-2 edit-folder" href="#" data-id="${folder.id}">
                <i class="bx bx-edit"></i> Edit</a></li>
              <li><a class="dropdown-item text-danger d-flex align-items-center gap-2 delete-folder" href="#" data-id="${folder.id}">
                <i class="bx bx-trash"></i> Delete</a></li>
            </ul>
          </div>
        </div>
      </div>
    `;
        col.querySelector('.card-body').addEventListener('click', () => {
            window.location.href = `/DocumentationSystem/_FolderDetail?id=${folder.id}`;
        });
        folderList.appendChild(col);
    });

    const totalPages = Math.ceil(list.length / folderPageSize);
    const prevBtn = document.getElementById('prev-btn');
    const nextBtn = document.getElementById('next-btn');
    if (paginationControls) {
        paginationControls.style.display = list.length > folderPageSize ? 'flex' : 'none';
        paginationControls.classList.add('justify-content-end', 'pe-4');
    }
    if (pageInfo) { pageInfo.textContent = `Page ${folderPage} of ${totalPages}`; pageInfo.style.display = 'block'; }
    if (prevBtn) prevBtn.disabled = folderPage === 1;
    if (nextBtn) nextBtn.disabled = folderPage >= totalPages;
}

/* -------------------- Documents ------------------- */
async function loadDocuments() {
    try {
        const json = await fetchJSON(
            `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/GetDocumentByFolderId?id=${currentFolderId}`,
            {}, 'Dosyalar getirilirken bir hata oluştu.'
        );
        documents = json?.data || [];
        renderDocuments(documents);
    } catch { }
}
function bindFilePaging() {
    const prev = document.getElementById('file-prev-btn');
    const next = document.getElementById('file-next-btn');
    prev && prev.addEventListener('click', () => {
        if (filePage > 1) { filePage--; renderDocuments(documents); }
    });
    next && next.addEventListener('click', () => {
        const totalPages = Math.ceil((documents?.length || 0) / filePageSize);
        if (filePage < totalPages) { filePage++; renderDocuments(documents); }
    });
}
function renderDocuments(list) {
    const tbody = document.getElementById('documentsContainer');
    if (!tbody) return;
    tbody.innerHTML = '';

    const startIndex = (filePage - 1) * filePageSize;
    const endIndex = Math.min(startIndex + filePageSize, list.length);
    const pageDocs = list.slice(startIndex, endIndex);

    if (pageDocs.length === 0) {
        tbody.innerHTML = `<tr><td colspan="9" class="text-center text-muted">No documents found.</td></tr>`;
        updateFilePagination(0, 0, 0);
        return;
    }

    pageDocs.forEach(doc => {
        const tr = document.createElement('tr');
        const tagsHtml = (doc.tags || []).map(t => `<span class="badge shadow-sm text-dark bg-white me-1 mb-2">${t}</span>`).join(' ');
        const created = formatDateWithUser(doc.createdDate, doc.createdBy);
        const modified = (doc.modifiedDate === doc.createdDate) ? '-' : formatDateWithUser(doc.modifiedDate, doc.modifiedBy);
        const statusColor = doc.status ? 'success' : 'secondary';

        tr.innerHTML = `
      <td><input type="checkbox" class="form-check-input" /></td>
      <td>
        <div class="d-flex flex-column">
          <div class="d-flex align-items-center gap-2">
            <span class="fw-semibold">${doc.documentName}</span>
            <i class="fa ${doc.isStar ? 'fa-star text-warning' : 'fa-star-o text-muted'}"></i>
          </div>
          <small class="text-muted">${doc.documentTitle || ''}</small>
          <small class="text-muted">${doc.description || ''}</small>
          <div class="mt-1 d-flex flex-wrap">${tagsHtml}</div>
        </div>
      </td>
      <td>${(doc.documentType || '').toUpperCase()}</td>
      <td>${doc.documentSize} MB</td>
      <td>${doc.folderName || '-'}</td>
      <td>${created}</td>
      <td>${modified}</td>
      <td>
        <span class="badge bg-${statusColor} bg-opacity-25 text-${statusColor} px-2 py-1 rounded">
          ${doc.status ? 'Active' : 'Inactive'}
        </span>
      </td>
      <td>
        <div class="dropdown">
          <i class='bx bx-dots-vertical-rounded text-muted fs-4 dropdown-toggle' data-bs-toggle="dropdown"></i>
          <ul class="dropdown-menu dropdown-menu-end shadow-sm">
            <li><a class="dropdown-item d-flex align-items-center gap-2" href="${doc.documentUrl}" target="_blank"><i class="fa fa-eye"></i> Preview</a></li>
            <li><a class="dropdown-item d-flex align-items-center gap-2 download-file" href="#" data-url="${doc.documentUrl}" data-name="${doc.documentName}"><i class="fa fa-download"></i> Download</a></li>
            <li><a class="dropdown-item d-flex align-items-center gap-2 star-document" href="#" data-id="${doc.id}" data-star="${doc.isStar}"><i class="fa fa-star"></i>${doc.isStar ? 'Unstar' : 'Star'}</a></li>
            <li><a class="dropdown-item d-flex align-items-center gap-2 open-share-document" href="#" data-id="${doc.id}" data-name="${doc.documentName}"><i class="fa fa-share"></i> Share</a></li>
            <li><a class="dropdown-item d-flex align-items-center gap-2 open-history-document" href="#" data-id="${doc.id}" data-name="${doc.documentName}"><i class="fa fa-history"></i> Version History</a></li>
            <li><a class="dropdown-item d-flex align-items-center gap-2" href="#" onclick="openEditDocument('${doc.id}')"><i class="fa fa-edit"></i> Edit</a></li>
            <li><a class="dropdown-item d-flex align-items-center gap-2 open-move-document" href="#" data-id="${doc.id}" data-name="${doc.documentName}" data-folder-name="${doc.folderName}"><i class="fa fa-arrows-alt"></i> Move</a></li>
            <li><a class="dropdown-item text-danger d-flex align-items-center gap-2 delete-file" href="#" data-id="${doc.id}"><i class="fa fa-trash"></i> Delete</a></li>
          </ul>
        </div>
      </td>
    `;
        tbody.appendChild(tr);
    });

    updateFilePagination(filePage, filePageSize, list.length);
}
function updateFilePagination(current, size, total) {
    const start = (current - 1) * size + 1;
    const end = Math.min(start + size - 1, total);
    const info = document.getElementById('file-page-info');
    if (info) info.textContent = `Showing ${total ? start : 0} to ${total ? end : 0} of ${total} entries`;
    const prev = document.getElementById('file-prev-btn');
    const next = document.getElementById('file-next-btn');
    if (prev) prev.disabled = current === 1;
    if (next) next.disabled = end >= total;
}

/* ---------------- Create Folder (visibility) -------------- */
function initCreateFolderVisibility() {
    const visibilitySelect = document.getElementById('create-folder-visibility');
    const userPermCnt = document.getElementById('user-permission-container');
    if (!visibilitySelect || !userPermCnt) return;

    userPermCnt.style.display = 'none';

    $(visibilitySelect).off('change.create').on('change.create', async function () {
        const val = $(this).val();
        if (val === 'restricted') {
            userPermCnt.style.display = 'block';
            await UserDirectory.populate('create-user-permission');
        } else {
            userPermCnt.style.display = 'none';
            const $sel = $('#create-user-permission');
            if ($sel.data('selectpicker')) $sel.selectpicker('val', []).selectpicker('refresh');
        }
    });

    const off = document.getElementById('CreateFolder');
    off && off.addEventListener('show.bs.offcanvas', () => {
        $('#create-folder-visibility').val('').trigger('change');
        userPermCnt.style.display = 'none';
        const $sel = $('#create-user-permission');
        if ($sel.data('selectpicker')) $sel.selectpicker('val', []).selectpicker('refresh');
    });
}
async function handleCreateFolderSubmit(e) {
    e.preventDefault();
    const name = document.getElementById('create-folder-name')?.value.trim();
    const description = document.getElementById('create-folder-description')?.value.trim();
    const visibility = document.getElementById('create-folder-visibility')?.value;
    const userName = window.getUserName?.();
    const userId = window.getUserId?.();
    if (!name) return showToast('Folder name is required.', 'error');

    const payload = { name, description, visibility, userId, parentId: currentFolderId, createdBy: userName };
    if (visibility === 'restricted') {
        const ids = $('#create-user-permission').val() || [];
        if (!ids.length) return showToast('Please select at least one user for restricted visibility.', 'error');
        payload.userIds = ids;
    }

    try {
        const res = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/CreateFolder`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
        });
        if (res.status !== 201) { showToast('Klasör oluşturulamadı. Lütfen tekrar deneyin.', 'error'); return; }

        const json = await res.json();
        const newId = json?.data;
        folders.push({ id: newId, ...payload, documentCount: 0, subFolderCount: 0, userIds: payload.userIds || [] });
        renderSubFolders(folders);

        const form = document.getElementById('createFolderForm');
        form && form.reset();
        $('#create-folder-visibility').val('').trigger('change');
        const $sel = $('#create-user-permission');
        if ($sel.data('selectpicker')) $sel.selectpicker('val', []).selectpicker('refresh');

        const panel = document.getElementById('CreateFolder');
        panel && bootstrap.Offcanvas.getOrCreateInstance(panel).hide();
        showToast('Folder created successfully.');
    } catch { }
}

/* ---------------- Edit / Delete Folder ------------------- */
function bindEditFolderEvent() {
    if (__wiredEditFolder) return;
    __wiredEditFolder = true;

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.edit-folder');
        if (!btn) return;

        const id = btn.getAttribute('data-id');
        const folder = folders.find(f => String(f.id) === String(id));
        if (!folder) return;

        const offEl = document.getElementById('EditFolder');
        const panel = bootstrap.Offcanvas.getOrCreateInstance(offEl);

        // doldur
        $('#edit-folder-id').val(folder.id);
        $('#edit-folder-name').val(folder.name);
        $('#edit-folder-description').val(folder.description || '');
        $('#edit-folder-visibility').val(folder.visibility).selectpicker('refresh');

        const $userSel = $('#edit-user-permission');

        async function applyVisibilityUI(value) {
            if (value === 'restricted') {
                $('#edit-user-permission-container').show();
                await UserDirectory.populate('edit-user-permission', { selected: [...new Set(folder.userIds || [])] });
            } else {
                $('#edit-user-permission-container').hide();
                if ($userSel.data('selectpicker')) $userSel.selectpicker('val', []).selectpicker('refresh');
            }
        }
        await applyVisibilityUI(folder.visibility);

        // change handler (panel ömrü boyunca)
        $('#edit-folder-visibility').off('change.edit').on('change.edit', async function () {
            await applyVisibilityUI($(this).val());
        });

        // submit
        $('#editFolderForm').off('submit.edit').on('submit.edit', async function (ev) {
            ev.preventDefault();
            const visibility = $('#edit-folder-visibility').val();
            const payload = {
                id: $('#edit-folder-id').val(),
                name: $('#edit-folder-name').val().trim(),
                description: $('#edit-folder-description').val().trim(),
                visibility,
                userIds: visibility === 'restricted' ? ($userSel.val() || []) : [],
                modifiedBy: window.getUserName?.()
            };
            try {
                const res = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/UpdateFolder`, {
                    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
                });
                const result = await res.json(); // ✔ JSON'u oku                
                if (res.ok && result.data) {
                    showToast('Folder updated successfully.');
                    panel.hide();
                    loadFolders();
                } else {
                    showToast(result.errors || 'Update failed.', 'error');
                }
            } catch {
                showToast('Update failed.', 'error');
            }
        });

        // kapandığında temizlik
        offEl.addEventListener('hidden.bs.offcanvas', () => {
            $('#edit-folder-visibility').off('change.edit');
            $('#editFolderForm').off('submit.edit');
            if ($userSel.data('selectpicker')) $userSel.selectpicker('val', []).selectpicker('refresh');
            $('#edit-user-permission-container').hide();
        }, { once: true });

        panel.show();
    });
}
let __wiredEditFolder = false;

function bindDeleteFolderEvent() {
    if (__wiredDeleteFolder) return;
    __wiredDeleteFolder = true;

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.delete-folder');
        if (!btn) return;
        folderIdToDelete = btn.getAttribute('data-id');
        new bootstrap.Modal(document.getElementById('deleteConfirmModal')).show();
    });

    const confirm = document.getElementById('confirmDeleteBtn');
    if (!confirm) return;
    confirm.addEventListener('click', async function () {
        if (!folderIdToDelete) return;
        try {
            const json = await fetchJSON(
                `${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/DeleteFolder`,
                { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: folderIdToDelete, modifiedBy: window.getUserName?.() }) },
                'Klasör silinemedi.'
            );
            if (json?.data) {
                folders = folders.filter(f => f.id !== folderIdToDelete);
                renderSubFolders(folders);
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
                showToast('Klasör başarıyla silindi.');
            }
        } catch { }
    });
}

/* ---------------- Delete Document ------------------------ */
function bindDeleteDocumentEvent() {
    if (__wiredDeleteFile) return;
    __wiredDeleteFile = true;

    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.delete-file');
        if (!btn) return;
        fileIdToDelete = btn.getAttribute('data-id');
        new bootstrap.Modal(document.getElementById('deleteConfirmModal')).show();
    });

    const confirm = document.getElementById('confirmDeleteBtn');
    if (!confirm) return;
    confirm.addEventListener('click', async function () {
        if (!fileIdToDelete) return;
        try {
            const json = await fetchJSON(
                `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/DeleteDocument`,
                { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: fileIdToDelete, modifiedBy: window.getUserName?.() }) },
                'Belge silinemedi.'
            );
            if (json?.data) {
                documents = documents.filter(d => d.id !== fileIdToDelete);
                renderDocuments(documents);
                bootstrap.Modal.getInstance(document.getElementById('deleteConfirmModal')).hide();
                showToast('Belge başarıyla silindi.');
            }
        } catch { }
    });
}

/* ---------------- Move Document -------------------------- */
function buildTreeUL(nodes, pathPrefix = '') {
    const ul = document.createElement('ul');
    nodes?.forEach(folder => {
        const li = document.createElement('li');
        const hasChildren = Array.isArray(folder.childFolders) && folder.childFolders.length > 0;
        const fullPath = `${pathPrefix}${folder.name}`;

        li.dataset.id = folder.id;
        li.dataset.name = folder.name;

        // satır
        const row = document.createElement('div');
        row.className = 'tree-row';

        // toggle
        const tog = document.createElement('span');
        tog.className = 'tree-toggle' + (hasChildren ? '' : ' disabled');
        tog.textContent = hasChildren ? '−' : '';
        tog.title = hasChildren ? 'Collapse/Expand' : '';
        row.appendChild(tog);

        // radio (sadece folder seçilsin)
        const radio = document.createElement('input');
        radio.type = 'radio';
        radio.name = 'destinationFolder';
        radio.value = folder.id;
        radio.dataset.path = fullPath;
        row.appendChild(radio);

        // ikon
        const icon = document.createElement('i');
        icon.className = hasChildren ? 'fa fa-folder-open' : 'fa fa-folder';
        row.appendChild(icon);

        // metin
        const label = document.createElement('span');
        label.textContent = folder.name;
        row.appendChild(label);

        li.appendChild(row);

        // children
        if (hasChildren) {
            const childUL = buildTreeUL(folder.childFolders, fullPath + '/');
            childUL.className = 'children';
            li.appendChild(childUL);
        }

        ul.appendChild(li);
    });
    return ul;
}

function renderFolderTree(rootData) {
    const container = document.getElementById('folderTree');
    container.innerHTML = '';
    const rootUL = buildTreeUL(rootData || []);
    rootUL.className = 'tree';
    container.appendChild(rootUL);

    attachTreeEvents(container);
}

function attachTreeEvents(container) {
    const currentPathEl = document.getElementById('current-folder-path');
    const hiddenIdEl = document.getElementById('selected-folder-id');
    const hiddenPathEl = document.getElementById('selected-folder-path');

    // Toggle / Satır tık / Radio change -> event delegation
    container.addEventListener('click', (e) => {
        const t = e.target;

        // Toggle
        if (t.classList.contains('tree-toggle') && !t.classList.contains('disabled')) {
            const li = t.closest('li');
            const ul = li.querySelector(':scope > ul.children');
            if (!ul) return;
            const open = ul.style.display !== 'none';
            ul.style.display = open ? 'none' : '';
            const icon = li.querySelector(':scope > .tree-row i.fa');
            if (icon) icon.className = open ? 'fa fa-folder' : 'fa fa-folder-open';
            t.textContent = open ? '+' : '−';
            return;
        }

        // Satıra tıklanınca radio seç
        const row = t.closest('.tree-row');
        if (row) {
            const radio = row.querySelector('input[type="radio"]');
            if (radio && !radio.disabled) {
                radio.checked = true;
                updateSelection(row.closest('li'));
            }
        }
    });

    container.addEventListener('change', (e) => {
        if (e.target.name === 'destinationFolder') {
            updateSelection(e.target.closest('li'));
        }
    });

    function updateSelection(li) {
        // seçili görselini temizle
        container.querySelectorAll('.tree-row.selected')
            .forEach(el => el.classList.remove('selected'));
        li.querySelector(':scope > .tree-row').classList.add('selected');

        // path hesapla
        const pathParts = [];
        let cur = li;
        while (cur && cur.matches('li')) {
            pathParts.unshift(cur.dataset.name);
            cur = cur.parentElement.closest('li');
        }
        const pathText = pathParts.join(' / ');

        // hidden + UI
        hiddenIdEl.value = li.dataset.id || '';
        hiddenPathEl.value = pathText;
        currentPathEl.textContent = pathText || 'Current Folder';
    }
}

// === Mevcut openMoveModal’ı güncelle ===
function openMoveModal(docId, docName, docFolderName) {
    document.getElementById('move-doc-id').value = docId;
    document.getElementById('move-doc-name').textContent = docName;
    document.getElementById('folderTree').innerHTML = "<div class='text-muted'></div>";
    document.getElementById('current-folder-path').textContent = docFolderName || 'Current Folder';
    document.getElementById('selected-folder-id').value = '';
    document.getElementById('selected-folder-path').value = '';

    const userId = window.getUserId?.();
    fetchJSON(`${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/GetFolderByUserId?id=${userId}`, {}, 'Klasörler alınamadı.')
        .then(result => {
            const root = result?.data || [];
            if (!root.length) {
                document.getElementById('folderTree').innerHTML = "<div class='text-warning'>No folders found.</div>";
                return;
            }
            renderFolderTree(root);
        }).catch(() => {
            document.getElementById('folderTree').innerHTML = "<div class='text-danger'>Failed to load folders.</div>";
        });

    new bootstrap.Modal(document.getElementById('MoveDocumentModal')).show();
}

function handleMoveSubmit(e) {
    e.preventDefault();
    const folderId = document.getElementById('selected-folder-id').value;
    const docId = document.getElementById('move-doc-id').value;
    if (!folderId) return showToast('Please select a folder.', 'error');

    const payload = { id: docId, folderId, modifiedBy: window.getUserName?.() };
    fetchJSON(
        `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/MoveDocument`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) },
        'Move failed.'
    ).then(() => {
        showToast('Document moved successfully.');
        bootstrap.Modal.getInstance(document.getElementById('MoveDocumentModal')).hide();
        loadDocuments();
    });
}

const moveForm = document.getElementById('moveDocumentForm');
if (moveForm) moveForm.addEventListener('submit', handleMoveSubmit);

/* ---------------- Version History ----------------------- */
function initVersionHistoryDeps() {
    const modalEl = document.getElementById('VersionHistoryModal');
    const tplEl = document.getElementById('version-card-template');
    if (!modalEl || !tplEl) { console.warn('VH modal/template yok'); return; }
    const tpl = $(tplEl).html();
    if (!tpl) { console.warn('VH template boş'); return; }
    vhDeps = { modal: bootstrap.Modal.getOrCreateInstance(modalEl), list: $('#version-list'), tpl };
}
async function openVersionHistory(documentId, documentName) {
    if (!vhDeps) initVersionHistoryDeps();
    if (!vhDeps) return showToast('Sürüm geçmişi bileşenleri hazır değil.', 'error');

    $('#version-doc-name').text(documentName || '-');
    vhDeps.list.html(`<div class="text-center py-4"><div class="spinner-border" role="status"></div></div>`);

    try {
        const json = await fetchJSON(
            `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/GetDocumentHistoryByDocumentId?id=${documentId}`,
            {}, 'Sürüm geçmişi yüklenemedi.'
        );
        const items = json?.data || [];
        if (!items.length) {
            vhDeps.list.html(`<div class="alert alert-light mb-0">Herhangi bir sürüm bulunamadı.</div>`);
        } else {
            renderVersionCards(vhDeps.list, vhDeps.tpl, items);
        }
    } catch {
        vhDeps.list.html(`<div class="alert alert-light mb-0">Herhangi bir sürüm bulunamadı.</div>`);
    }

    vhDeps.modal.show();
}
function renderVersionCards($container, tpl, items) {
    if (!tpl) return;
    $container.empty();
    items.sort((a, b) => new Date(b.createdDate) - new Date(a.createdDate));
    items.forEach(it => {
        const vrs = `${it.version}.0`;
        const size = it.documentSize ? `${it.documentSize} MB` : '';
        const html = tpl
            .replace(/\{\{version\}\}/g, vrs)
            .replace(/\{\{size\}\}/g, size)
            .replace(/\{\{uploadedBy\}\}/g, (it.createdBy || '-'))
            .replace(/\{\{uploadedDate\}\}/g, formatDate(it.createdDate))
            .replace(/\{\{description\}\}/g, (it.description || ''));
        const $card = $(html);
        const hasUrl = !!it.documentUrl;

        $card.find('.preview-btn').prop('disabled', !hasUrl).toggleClass('disabled', !hasUrl).on('click', (e) => {
            e.preventDefault();
            if (!hasUrl) return showToast('Önizleme için dosya yolu bulunamadı.', 'error');
            window.open(it.documentUrl, '_blank');
        });

        $card.find('.download-btn').on('click', async (e) => {
            e.preventDefault();
            if (!hasUrl) return showToast('İndirme için dosya yolu bulunamadı.', 'error');
            try {
                const res = await fetch(it.documentUrl);
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const blob = await res.blob();
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = (it.documentName || 'version') + getExtFromUrl(it.documentUrl);
                document.body.appendChild(a);
                a.click();
                window.URL.revokeObjectURL(url);
                a.remove();
            } catch { showToast('Dosya indirilemedi.', 'error'); }
        });

        $container.append($card);
    });
}

/* ---------------- Delegated Clicks ---------------------- */
function delegatedClicks(e) {
    const moveBtn = e.target.closest('.open-move-document');
    if (moveBtn) {
        e.preventDefault();
        openMoveModal(
            moveBtn.getAttribute('data-id'),
            moveBtn.getAttribute('data-name'),
            moveBtn.getAttribute('data-folder-name')
        );
        return;
    }

    const downloadBtn = e.target.closest('.download-file');
    if (downloadBtn) {
        e.preventDefault();
        const fileUrl = downloadBtn.getAttribute('data-url');
        const fileName = downloadBtn.getAttribute('data-name') || 'download';
        if (!fileUrl) return showToast('Dosya yolu bulunamadı.', 'error');
        fetch(fileUrl)
            .then(res => { if (!res.ok) throw new Error('Network response was not ok.'); return res.blob(); })
            .then(blob => {
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url; a.download = fileName;
                document.body.appendChild(a); a.click(); window.URL.revokeObjectURL(url); a.remove();
            })
            .catch(() => showToast('Dosya indirilemedi.', 'error'));
        return;
    }

    const starBtn = e.target.closest('.star-document');
    if (starBtn) {
        e.preventDefault();
        const docId = starBtn.getAttribute('data-id');
        const docStar = starBtn.getAttribute('data-star'); // "true"/"false"
        const payload = { id: docId, isStar: docStar !== 'true', modifiedBy: window.getUserName?.() };
        fetchJSON(
            `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/StarDocument`,
            { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) },
            'Star failed.'
        ).then(() => loadDocuments());
        return;
    }

    const vhBtn = e.target.closest('.open-history-document');
    if (vhBtn) {
        e.preventDefault(); e.stopPropagation();
        openVersionHistory(vhBtn.getAttribute('data-id'), vhBtn.getAttribute('data-name'));
        return;
    }

    const shareBtn = e.target.closest('.open-share-document');
    if (shareBtn) {
        e.preventDefault();
        openShareModal(shareBtn.getAttribute('data-id'), shareBtn.getAttribute('data-name'));
        return;
    }
}
if (!__wiredDelegated) {
    document.addEventListener('click', delegatedClicks);
    __wiredDelegated = true;
}

/* ---------------- Share (3 tab, tek endpoint) ------------ */
function openShareModal(docId, docName) {
    // başlık/ids
    $('#share-doc-name').text(docName || '-');
    $('#share-doc-id').val(docId);
    $('#share-email-doc-id').val(docId);
    $('#share-internal-doc-id').val(docId);

    // link tab reset
    $('#share-link-url').val('');
    $('#share-access-level').val('View Only'); // disabled text
    $('#share-expiry-date').val('');
    $('#share-password').val('');

    // email tab reset
    $('#share-email-input').val('');
    $('#share-email-message').val('');
    $('#share-email-selected').empty();

    // internal users — cache’den tek sefer populate (tamamen replace)
    UserDirectory.populate('share-internal-users', { selected: [] });

    // ilk tab
    const firstTabBtn = document.querySelector('#tab-link-tab');
    if (firstTabBtn) new bootstrap.Tab(firstTabBtn).show();

    // modal
    const modalEl = document.getElementById('ShareDocumentModal');
    bootstrap.Modal.getOrCreateInstance(modalEl).show();

    // kapandığında seçimleri sıfırla (options kalır, duplication olmaz)
    modalEl.addEventListener('hidden.bs.modal', () => {
        $('#share-link-url,#share-expiry-date,#share-password,#share-email-input,#share-email-message').val('');
        $('#share-email-selected').empty();
    }, { once: true });

    // eventleri tek kez bağla
    initShareOnce();
}
function initShareOnce() {
    if (__wiredShareInit) return;
    __wiredShareInit = true;

    // Link form
    const linkForm = document.getElementById('shareLinkForm');
    const copyBtn = document.getElementById('btn-copy-link');
    if (linkForm) {
        linkForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const docId = $('#share-doc-id').val();
            const expiry = $('#share-expiry-date').val() || null;
            const pass = $('#share-password').val() || null;
            if (!docId) return showToast('Document id is missing.', 'error');

            const payload = {
                documentId: docId,
                access: 'ViewOnly',
                expiresAt: expiry ? new Date(expiry).toISOString() : null,
                passwordPlain: pass || null,
                sendEmail: false,
                emailRecipients: [],
                userIds: [],
                message: null
            };
            try {
                const json = await fetchJSON(
                    `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/Share`,
                    { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) },
                    'Share failed.'
                );
                const url = json?.data?.shareUrl || '';
                if (!url) return showToast('Share URL üretilemedi.', 'error');
                $('#share-link-url').val(url);
                showToast('Share link created.');
            } catch { }
        });
    }
    if (copyBtn) {
        copyBtn.addEventListener('click', async () => {
            const url = $('#share-link-url').val();
            if (!url) return showToast('No link to copy.', 'warning');
            try { await navigator.clipboard.writeText(url); showToast('Link copied to clipboard.'); }
            catch { showToast('Failed to copy.', 'error'); }
        });
    }

    // Email form
    const emailForm = document.getElementById('shareEmailForm');
    const emailInput = document.getElementById('share-email-input');
    const addBtn = document.getElementById('btn-add-email');
    const chipsHolder = document.getElementById('share-email-selected');
    const isValidEmail = (v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);

    function addEmailChip(email) {
        const exists = Array.from(chipsHolder.querySelectorAll('.share-email-chip'))
            .some(el => (el.dataset.email || '').toLowerCase() === email.toLowerCase());
        if (exists) { showToast('This email is already added.', 'warning'); return; }
        const chip = document.createElement('span');
        chip.className = 'badge bg-light text-dark border px-2 py-1 d-flex align-items-center share-email-chip';
        chip.dataset.email = email;
        chip.innerHTML = `<span>${email}</span><i class="fa fa-times ms-2" role="button" title="Remove"></i>`;
        chip.querySelector('.fa-times').addEventListener('click', () => chip.remove());
        chipsHolder.appendChild(chip);
    }
    function addEmailFromInput() {
        const raw = (emailInput?.value || '').trim();
        if (!raw) return;
        const parts = raw.split(',').map(s => s.trim()).filter(Boolean);
        let pushed = false;
        for (const e of parts) {
            if (!isValidEmail(e)) { showToast(`Invalid email: ${e}`, 'error'); continue; }
            addEmailChip(e); pushed = true;
        }
        if (pushed) emailInput.value = '';
    }
    if (addBtn && emailInput && chipsHolder) {
        addBtn.addEventListener('click', addEmailFromInput);
        emailInput.addEventListener('keydown', (ev) => {
            if (ev.key === 'Enter') { ev.preventDefault(); addEmailFromInput(); }
        });
    }
    if (emailForm) {
        emailForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const docId = $('#share-email-doc-id').val();
            if (!docId) return showToast('Document id is missing.', 'error');
            const emails = Array.from(chipsHolder.querySelectorAll('.share-email-chip'))
                .map(el => el.dataset.email).filter(Boolean);
            if (!emails.length) return showToast('Please add at least one recipient.', 'warning');
            const message = ($('#share-email-message').val() || '').toString().trim() || null;

            const payload = {
                documentId: docId,
                access: 'ViewOnly',
                expiresAt: null,
                passwordPlain: null,
                sendEmail: true,
                emailRecipients: emails,
                userIds: [],
                message
            };
            try {
                await fetchJSON(
                    `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/Share`,
                    { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) },
                    'Share failed.'
                );
                showToast('Document shared via email.');
            } catch { }
        });
    }

    // Internal Users form
    const internalForm = document.getElementById('shareInternalForm');
    if (internalForm) {
        internalForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const docId = $('#share-internal-doc-id').val();
            if (!docId) return showToast('Document id is missing.', 'error');
            const $sel = $('#share-internal-users');
            const userIds = $sel.data('selectpicker') ? ($sel.val() || []) : [];
            if (!userIds.length) return showToast('Please select at least one user.', 'warning');

            const payload = {
                documentId: docId,
                access: 'ViewOnly',
                expiresAt: null,
                passwordPlain: null,
                sendEmail: false,
                emailRecipients: [],
                userIds,
                message: null
            };
            try {
                await fetchJSON(
                    `${window.ApiBaseUrl}/services/PvDocumentManagement/Document/Share`,
                    { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) },
                    'Share failed.'
                );
                showToast('Document shared with selected users.');
            } catch { }
        });
    }
}

/* --------------- Edit Document Nav ---------------------- */
function openEditDocument(documentId) {
    if (!documentId) return;
    window.location.href = `/DocumentationSystem/_EditDocument?id=${documentId}`;
}

