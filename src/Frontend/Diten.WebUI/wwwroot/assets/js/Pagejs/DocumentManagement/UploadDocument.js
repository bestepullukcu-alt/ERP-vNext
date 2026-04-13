const domain = window.location.hostname;
const protocol = window.location.protocol;
const port = protocol === 'https:' ? '5003' : '5000';

document.addEventListener("DOMContentLoaded", async () => {
    const urlParams = new URLSearchParams(window.location.search);
    const folderId = urlParams.get("id");

    const userId = window.getUserId();
    const elements = getDomElements();

    //breadcrumb
    const breadcrumbContainer = document.querySelector(".breadcrumb");
    const folderPath = await getFolderPath(folderId);

    // Dinamik klasör yolu
    folderPath.forEach((folder, index) => {
            breadcrumbContainer.innerHTML += `
                    <li class="breadcrumb-item"><a href="/DocumentationSystem/_FolderDetail?id=${folder.id}">${folder.name}</a></li>
                `;
    });

    setupFilePreviewHandlers(elements);
    fetchFolders(domain, userId, folderId, elements.folderSelect);
    setupTagInput(elements);
    setupFormSubmit(domain, userId, elements, folderId);
});

// DOM Elemanlarını toplayan yardımcı fonksiyon
function getDomElements() {
    return {
        chooseFileBtn: document.getElementById("chooseFileBtn"),
        fileInput: document.getElementById("fileUpload"),
        dropDefault: document.getElementById("dropDefault"),
        filePreview: document.getElementById("filePreview"),
        fileName: document.getElementById("fileName"),
        fileSize: document.getElementById("fileSize"),
        fileType: document.getElementById("fileType"),
        removeFileBtn: document.getElementById("removeFileBtn"),
        replaceFileBtn: document.getElementById("replaceFileBtn"),
        folderSelect: document.getElementById("folderSelect"),
        tagInput: document.getElementById("tagInput"),
        addTagBtn: document.getElementById("addTagBtn"),
        tagContainer: document.getElementById("tagContainer"),
        form: document.getElementById("uploadDocumentForm")
    };
}
async function getFolderPath(folderId) {
    let path = [];
    let currentFolderId = folderId;

    while (currentFolderId) {
        const res = await fetch(`${protocol}//${domain}:${port}/services/PvDocumentManagement/Folder/GetFolderById?id=${currentFolderId}`);
        const data = await res.json();

        if (!data?.data) break;

        path.unshift({
            id: data.data.id,
            name: data.data.name
        });

        currentFolderId = data.data.parentId || null;
    }

    return path;
}

// Dosya seçme, ön izleme ve temizleme işlemleri
function setupFilePreviewHandlers({ chooseFileBtn, fileInput, dropDefault, filePreview, fileName, fileSize, fileType, removeFileBtn, replaceFileBtn }) {
    chooseFileBtn.addEventListener("click", () => fileInput.click());
    replaceFileBtn.addEventListener("click", () => fileInput.click());

    removeFileBtn.addEventListener("click", () => {
        fileInput.value = "";
        dropDefault.classList.remove("d-none");
        filePreview.classList.add("d-none");
    });

    fileInput.addEventListener("change", (e) => {
        const file = e.target.files[0];
        if (!file) return;

        fileName.textContent = file.name;
        fileSize.textContent = `${(file.size / 1024 / 1024).toFixed(2)} MB`;
        fileType.textContent = `Type: ${file.type}`;

        dropDefault.classList.add("d-none");
        filePreview.classList.remove("d-none");
    });
}

// Klasörleri API'den al ve select'e ekle
function fetchFolders(domain, userId, selectedFolderId, folderSelect) {
    fetch(`${protocol}//${domain}:${port}/services/PvDocumentManagement/Folder/GetFolderByUserId?id=${userId}`)
        .then(res => res.json())
        .then(data => {
            if (!Array.isArray(data.data)) return;

            const allFolders = [];

            function recursiveFlatten(folderList) {
                folderList.forEach(folder => {
                    // root (My Drive) gibi parentId boş olanları ekleme
                    const isRoot = !folder.parentId || folder.parentId.trim() === "";
                    if (!isRoot) {
                        allFolders.push(folder);
                    }

                    if (Array.isArray(folder.childFolders) && folder.childFolders.length > 0) {
                        recursiveFlatten(folder.childFolders);
                    }
                });
            }

            recursiveFlatten(data.data);

            allFolders.forEach(folder => {
                const option = document.createElement("option");
                option.value = folder.id;
                option.textContent = folder.folderPath;
                folderSelect.appendChild(option);
            });

            if (selectedFolderId) {
                folderSelect.value = selectedFolderId;
            }
        })
        .catch(err => console.error("Folder fetch error:", err));
}

// Tag ekleme işlemleri
function setupTagInput({ tagInput, addTagBtn, tagContainer }) {
    function createTagElement(tagValue) {
        const trimmed = tagValue.trim().toLowerCase();
        const span = document.createElement("span");

        span.className = "custom-tag d-inline-flex align-items-center";
        span.setAttribute("data-tag", trimmed);
        span.style.cssText = `
            background-color: #f1f2f4;
            color: #4b5563;
            font-weight: 500;
            border-radius: 12px;
            padding: 6px 12px;
            margin: 4px 8px 0 0;
            font-size: 14px;
        `;

        const label = document.createElement("span");
        label.textContent = trimmed;

        const removeBtn = document.createElement("button");
        removeBtn.type = "button";
        removeBtn.innerHTML = "&times;";
        removeBtn.style.cssText = `
            background: none;
            border: none;
            color: #6b7280;
            font-size: 16px;
            margin-left: 8px;
            cursor: pointer;
        `;
        removeBtn.setAttribute("aria-label", "Remove tag");
        removeBtn.addEventListener("click", () => span.remove());

        span.appendChild(label);
        span.appendChild(removeBtn);
        return span;
    }

    function addTag() {
        const tagValue = tagInput.value.trim().toLowerCase();
        if (!tagValue) return;

        const isDuplicate = Array.from(tagContainer.children)
            .some(t => t.getAttribute("data-tag") === tagValue);

        if (isDuplicate) {
            const originalPlaceholder = tagInput.placeholder;
            tagInput.value = "";
            tagInput.placeholder = "This tag is already added";
            tagInput.classList.add("border-danger");

            setTimeout(() => {
                tagInput.placeholder = originalPlaceholder;
                tagInput.classList.remove("border-danger");
            }, 2000);
            return;
        }

        tagContainer.appendChild(createTagElement(tagValue));
        tagInput.value = "";
    }

    addTagBtn.addEventListener("click", addTag);

    tagInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
            e.preventDefault();
            addTag();
        }
    });
}

// Form gönderme
function setupFormSubmit(domain, userId, { form, fileInput, filePreview, dropDefault, folderSelect, tagContainer }, selectedFolderId) {
    form.addEventListener("submit", async function (e) {
        e.preventDefault();

        if (!fileInput.files[0]) {
            alert("Please select a file.");
            return;
        }

        const formData = new FormData();

        const tags = Array.from(tagContainer.children).map(t => t.getAttribute("data-tag"));
        tags.forEach(tag => formData.append("Tags", tag));

        const file = fileInput.files[0];
        const folderIdToUse = folderSelect.value || selectedFolderId;
        const userName = window.getUserName();

        formData.append("File", file);
        formData.append("FolderId", folderIdToUse);
        formData.append("DocumentName", file.name.replace(/\.[^/.]+$/, ""));
        formData.append("DocumentType", file.type);
        formData.append("DocumentSize", (file.size / 1024 / 1024).toFixed(2));
        formData.append("UserId", userId);
        formData.append("DocumentTitle", document.getElementById("documentTitle").value);
        formData.append("AccessLevel", document.getElementById("accessLevel").value);
        formData.append("ExpireDate", document.getElementById("expiryDate").value);
        formData.append("Description", document.getElementById("notes").value);
        formData.append("CreatedBy", userName);

        try {
            const response = await fetch(`${protocol}//${domain}:${port}/services/PvDocumentManagement/Document/CreateDocument`, {
                method: "POST",
                body: formData
            });

            const result = await response.json();
            if (result?.data && result?.errors == null) {
                showToast("Document uploaded successfully!", 'success');
                form.reset();
                fileInput.value = "";
                filePreview.classList.add("d-none");
                dropDefault.classList.remove("d-none");
                tagContainer.innerHTML = "";

                setTimeout(() => {
                    window.location.href = `/DocumentationSystem/_FolderDetail?id=${folderIdToUse}`;
                }, 1000);
            } else {
                showToast("Upload failed: " + (result.message || "Unknown error"), 'error');
            }
        } catch (err) {
            console.error("Upload error:", err);
            showToast("An error occurred while uploading.", 'error');
        }
    });
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
