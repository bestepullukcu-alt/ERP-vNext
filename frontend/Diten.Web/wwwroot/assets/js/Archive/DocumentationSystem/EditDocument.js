let initialFileInfo = {
    name: "",
    size: "",
    type: "",
    folderId: ""
};

document.addEventListener("DOMContentLoaded", async () => {
    const documentId = new URLSearchParams(window.location.search).get("id");
    if (!documentId) return;

    try {
        const res = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Document/GetDocumentById?id=${documentId}`);
        const data = await res.json();
        if (!data.data) return showToast("Document not found", "error");
        fillEditForm(data.data);
    } catch (err) {
        console.error(err);
        showToast("Failed to fetch document.", "error");
    }

    //breadcrumb

    const breadcrumbContainer = document.querySelector(".breadcrumb");

    const folderPath = await getFolderPath(initialFileInfo.folderId);

    // Dinamik klasör yolu
    folderPath.forEach((folder, index) => {

            breadcrumbContainer.innerHTML += `
                    <li class="breadcrumb-item"><a href="/DocumentationSystem/_FolderDetail?id=${folder.id}">${folder.name}</a></li>
                `;
    });
});

async function getFolderPath(folderId) {
    let path = [];
    let currentFolderId = folderId;

    while (currentFolderId) {
        const res = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Folder/GetFolderById?id=${currentFolderId}`);
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

function fillEditForm(doc) {
    $("#documentTitle").val(doc.documentTitle || "");
    $("#notes").val(doc.description || "");
    $("#expiryDate").val(doc.expireDate ? doc.expireDate.split("T")[0] : "");
    $("#accessLevel").val(doc.accessLevel || "private").trigger("change");

    $("#folderNameDisplay").val(doc.folderName || "")
        .prop("disabled", true)
        .css({ color: "#6c757d", cursor: "not-allowed" });

    if (Array.isArray(doc.tags)) {
        $("#tagContainer").empty();
        doc.tags.forEach(appendTag);
    }

    if (doc.documentName && doc.documentSize && doc.documentType) {
        $("#filePreview").removeClass("d-none");
        $("#dropDefault").addClass("d-none");
        $("#fileName").text(doc.documentName);
        $("#fileSize").text(`${Number(doc.documentSize).toFixed(2)} MB`);
        $("#fileType").text(doc.documentType);

        // 📌 Orijinal dosya bilgilerini sakla
        initialFileInfo = {
            name: doc.documentName,
            size: doc.documentSize,
            type: doc.documentType,
            folderId: doc.folderId
        };
    }
}

function appendTag(tag) {
    const trimmed = tag.trim();
    if (!trimmed) return;

    const exists = Array.from($("#tagContainer .custom-tag")).some(t => $(t).data("tag") === trimmed);
    if (exists) return;

    const tagHtml = `
        <span class="custom-tag d-inline-flex align-items-center" data-tag="${trimmed}" 
              style="color:#4b5563;font-weight:500;border-radius:12px;padding:6px 12px;margin:4px 8px 0 0;font-size:14px;">
            ${trimmed}
            <button type="button" class="remove-tag-btn" style="background:none;border:none;color:#6b7280;font-size:16px;margin-left:8px;cursor:pointer;" aria-label="Remove tag">&times;</button>
        </span>`;
    $("#tagContainer").append(tagHtml);
}

$(document).on("click", ".remove-tag-btn", function () {
    $(this).closest(".custom-tag").remove();
});

$("#addTagBtn").on("click", () => {
    const tagVal = $("#tagInput").val().trim();
    if (tagVal) {
        appendTag(tagVal);
        $("#tagInput").val("");
    }
});

$("#tagInput").on("keypress", function (e) {
    if (e.which === 13) {
        e.preventDefault();
        const tagVal = $(this).val().trim();
        if (tagVal) {
            appendTag(tagVal);
            $(this).val("");
        }
    }
});

$("#chooseFileBtn, #replaceFileBtn").on("click", () => $("#fileUpload").trigger("click"));

$("#fileUpload").on("change", function (e) {
    const file = e.target.files[0];
    if (!file) return;

    $("#filePreview").removeClass("d-none");
    $("#dropDefault").addClass("d-none");

    $("#fileName").text(file.name);
    $("#fileSize").text((file.size / 1024 / 1024).toFixed(2) + " MB");
    $("#fileType").text(file.type);
});

$("#removeFileBtn").on("click", () => {
    $("#fileUpload").val("");
    $("#dropDefault").removeClass("d-none");
    $("#filePreview").addClass("d-none");
});

$("#uploadDocumentForm").on("submit", async function (e) {
    e.preventDefault();

    const documentId = new URLSearchParams(window.location.search).get("id");
    const formData = new FormData();

    const fileInput = document.getElementById("fileUpload");
    const file = fileInput.files[0];

    if (file) {
        formData.append("File", file);
        formData.append("DocumentSize", (file.size / 1024 / 1024).toFixed(2));
        formData.append("DocumentType", file.type);
        formData.append("DocumentName", file.name.replace(/\.[^/.]+$/, ""));
    } else {
        formData.append("DocumentSize", initialFileInfo.size || "");
        formData.append("DocumentType", initialFileInfo.type || "");
        formData.append("DocumentName", initialFileInfo.name || "");
    }

    const tags = Array.from(tagContainer.children).map(tag => tag.dataset.tag);
    tags.forEach(tag => formData.append("Tags", tag));

    formData.append("Id", documentId);
    formData.append("FolderId", initialFileInfo.folderId || "");
    formData.append("UserId", window.getUserId());
    formData.append("DocumentTitle", $("#documentTitle").val() ?? "");
    formData.append("AccessLevel", $("#accessLevel").val() ?? "");
    formData.append("ExpireDate", $("#expiryDate").val() ?? "");
    formData.append("Description", $("#notes").val() ?? "");
    formData.append("ModifiedBy", window.getUserName());

    try {
        const res = await fetch(`${window.ApiBaseUrl}/services/PvDocumentManagement/Document/UpdateDocument`, {
            method: "POST",
            body: formData
        });

        const result = await res.json();

        if (res.ok) {
            showToast("Document updated successfully.");
            setTimeout(() => {
                window.location.href = `/DocumentationSystem/_FolderDetail?id=${initialFileInfo.folderId}`;
            }, 1000);
        } else {
            showToast(result.message || "Update failed.", "error");
        }
    } catch (err) {
        console.error(err);
        showToast("Update request failed.", "error");
    }
});

function showToast(message, type = 'success') {
    const toastEl = document.getElementById('appToast');
    if (!toastEl) return;

    const toastBody = toastEl.querySelector('.toast-body');
    const toastHeader = toastEl.querySelector('#appToastHeader');

    toastBody.innerHTML = message;
    toastEl.classList.remove('bg-success', 'bg-danger', 'bg-warning', 'bg-info');

    switch (type) {
        case 'success': toastEl.classList.add('bg-success'); toastHeader.textContent = 'Success'; break;
        case 'error': toastEl.classList.add('bg-danger'); toastHeader.textContent = 'Error'; break;
        case 'warning': toastEl.classList.add('bg-warning'); toastHeader.textContent = 'Warning'; break;
        case 'info': toastEl.classList.add('bg-info'); toastHeader.textContent = 'Information'; break;
    }

    bootstrap.Toast.getOrCreateInstance(toastEl).show();
}
