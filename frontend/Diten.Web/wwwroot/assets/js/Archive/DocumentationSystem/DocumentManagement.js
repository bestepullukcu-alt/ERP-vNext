// ✅ Global State
let folders = [];
let currentPage = 1;
const pageSize = 8;
document.addEventListener('DOMContentLoaded', async function () {
    document.getElementById('prev-btn').addEventListener('click', () => {
        if (currentPage > 1) {
            currentPage--;
            renderFolders(folders);
        }
    });

    document.getElementById('next-btn').addEventListener('click', () => {
        const totalPages = Math.ceil(folders.length / pageSize);
        if (currentPage < totalPages) {
            currentPage++;
            renderFolders(folders);
        }
    });

    await handleInitialCreateFolder();
});
function openFolder(folderId) {
    if (!folderId)
        return;
    window.location.href = `/DocumentationSystem/_FolderDetail?id=${folderId}`;
}

async function handleInitialCreateFolder() {

    const userName = window.getUserName();
    const userId = window.getUserId();
    const name = userName;

    if (!name) {
        alert('Folder name is required.');
        return;
    }

    const newFolder = {
        name,
        description: "",
        visibility: "",
        userId,
        userIds: [],
        createdBy: userName
    };

    const response = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/CreateFolder`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newFolder)
    });

    if (response.status !== 201) {
        showToast('Klasör oluşturulamadı. Lütfen tekrar deneyin.', 'error');
        return;
    }

    await getFolders();
}

async function getFolders() {
    const userId = window.getUserId();
    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/GetFolderByUserId?id=${userId}`);

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Sunucu hatası: ${response.status} - ${errorText}`);
        }

        const result = await response.json();
        folders = result.data || [];
        renderFolders(folders);

    } catch (error) {
        showToast('Klasörler getirilirken bir hata oluştu.', "error");
    }
}

function renderFolders(folders) {
    const emptyView = document.getElementById('folder-empty');
    const folderList = document.getElementById('folder-list');
    const paginationControls = document.getElementById('pagination-controls');
    const pageInfo = document.getElementById('page-info');

    folderList.innerHTML = '';

    if (!folders || folders.length === 0) {
        emptyView.style.display = 'block';
        folderList.style.display = 'none';
        paginationControls.style.display = 'none';
        if (pageInfo) pageInfo.style.display = 'none';
    } else {
        emptyView.style.display = 'none';
        folderList.style.display = 'flex';
        folderList.classList.add('row');

        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = startIndex + pageSize;
        const paginatedFolders = folders.slice(startIndex, endIndex);

        paginatedFolders.forEach(folder => {
            let visibilityIcon = "";
            switch (folder.visibility) {
                case "public":
                    visibilityIcon = '<i class="fa fa-users text-warning" style="font-size: 0.8rem;"></i>';
                    break;
                case "private":
                    visibilityIcon = "<i class='bx bx-lock text-danger'></i>";
                    break;
                case "restricted":
                    visibilityIcon = '<i class="fa fa-users text-primary" style="font-size: 0.8rem;"></i>';
                    break;
            }

            const col = document.createElement("div");
            col.className = "col-12 col-md-3 mb-3 position-relative";

            col.innerHTML = `
              <div class="card h-100 d-flex flex-row align-items-center position-relative folder-card p-2"
                   style="cursor: pointer; border: 1px solid #e0e0e0; border-radius: 10px; box-shadow: 0 1px 2px rgba(0,0,0,0.04);">
                <div class="ps-2 pe-2 fs-4 text-primary">
                  <i class='bx bx-folder'></i>
                </div>
                <div class="card-body p-2">
                  <h6 class="card-title fw-semibold mb-1 d-flex align-items-center gap-1">
                    ${folder.name}
                    <span class="ms-1">${visibilityIcon}</span>
                  </h6>
                  <p class="card-text text-muted mb-1 small">${folder.description}</p>
                  <small class="text-muted">${folder.documentCount} documents • ${folder.subFolderCount} folders</small>
                </div>
              </div>
            `;

            col.querySelector('.card-body').addEventListener('click', () => {
                openFolder(folder.id);
            });

            folderList.appendChild(col);
        });


        const totalPages = Math.ceil(folders.length / pageSize);
        const prevBtn = document.getElementById("prev-btn");
        const nextBtn = document.getElementById("next-btn");

        paginationControls.style.display = folders.length > pageSize ? 'flex' : 'none';
        paginationControls.classList.add('justify-content-end', 'pe-4');

        if (pageInfo) {
            pageInfo.textContent = `Page ${currentPage} of ${totalPages}`;
            pageInfo.style.display = 'block';
        }

        if (prevBtn) prevBtn.disabled = currentPage === 1;
        if (nextBtn) nextBtn.disabled = currentPage >= totalPages;
    }
}

function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    const toastBody = toastEl.querySelector('.toast-body');
    const toastHeader = toastEl.querySelector('#appToastHeader');

    if (!toastEl || !toastBody || !toastHeader) return;

    toastBody.innerHTML = message;

    toastEl.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');

    switch (type) {
        case 'success':
            toastEl.classList.add('bg-success');
            toastHeader.textContent = 'Successfull';
            break;
        case 'error':
            toastEl.classList.add('bg-danger');
            toastHeader.textContent = 'Error';
            break;
        case 'warning':
            toastEl.classList.add('bg-warning');
            toastHeader.textContent = 'Warning';
            break;
        case 'info':
            toastEl.classList.add('bg-info');
            toastHeader.textContent = 'Information';
            break;
    }

    const toast = bootstrap.Toast.getOrCreateInstance(toastEl);
    toast.show();
}
