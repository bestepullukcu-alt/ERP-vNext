'use strict';
 protocol = window.location.protocol;
 domain = window.location.hostname;
 port = protocol === 'https:' ? '5055' : '5050';

document.addEventListener('DOMContentLoaded', function () {


    const sendButton = document.querySelector(".email-send-btn");
    if (!sendButton) {
        console.error("sendButton butonu DOM'da bulunamadı.");
        return;
    }

    sendButton.addEventListener("click", function (e) {
        e.preventDefault();
        sendEmail();
    });

    setupFileAttachmentPreview();


});


async function sendEmail() {
    const emailForm = document.querySelector(".email-compose-form");
    const userId = window.getUserId();

    const to = $("#emailContacts").val() || []; // Select2 destekli
    const ccList = $("#email-cc").val() || [];
    const bccList = $("#email-bcc").val() || [];

    const subject = document.getElementById("email-subject").value;
    const message = document.querySelector(".email-editor").innerHTML;
    const fileInput = document.getElementById("attach-file");

    const formData = new FormData();
    formData.append("FromUserId", userId);
    formData.append("To", to);
    ccList.forEach(email => formData.append("Cc", email));
    bccList.forEach(email => formData.append("Bcc", email));
    formData.append("Subject", subject);
    formData.append("Body", message);

    // Çoklu dosya destekliyorsa döngüyle eklenebilir
    if (fileInput.files.length > 0) {
        for (let i = 0; i < fileInput.files.length; i++) {
            formData.append("Attachments", fileInput.files[i]);
        }
    }

    const url = `${protocol}//${domain}:${port}/api/PvUser/Email/send-email`;

    try {
        const response = await fetch(url, {
            method: "POST",
            body: formData,
            headers: {
                'Authorization': 'Bearer ' + localStorage.getItem('token')
            }
        });
        const result = await response.json();
        if (result.data === true) {

            showToast('Your email has been sent successfully.', "success");
            emailForm.reset();
        }
        else {
            let errorText;
            try {
                const error = await response.json();
                errorText = error.message || "Server error";
            } catch {
                errorText = "The server did not return a valid error message.";
            }
            showToast("Sending failed: " + errorText, "error");

        }


        
    } catch (error) {
        console.error("API Hatası:", error);
        showToast("Oops! Something went wrong. Try again later.", "error");

    }
}

function setupFileAttachmentPreview(inputId = "attach-file", previewId = "attachment-preview") {
    const fileInput = document.getElementById(inputId);
    const preview = document.getElementById(previewId);

    if (!fileInput || !preview) {
        console.warn(`Element not found: inputId=${inputId}, previewId=${previewId}`);
        return;
    }

    fileInput.addEventListener("change", function (event) {
        preview.innerHTML = "";
        const files = Array.from(event.target.files);

        if (files.length === 0) {
            preview.textContent = "No file selected.";
            return;
        }

        const list = document.createElement("ul");
        list.classList.add("list-unstyled", "mb-0");

        files.forEach((file, index) => {
            const item = document.createElement("li");
            item.classList.add("d-flex", "align-items-center", "mb-2");

            // Resim önizleme
            if (file.type.startsWith("image/")) {
                const img = document.createElement("img");
                img.src = URL.createObjectURL(file);
                img.style.width = "40px";
                img.style.height = "40px";
                img.style.objectFit = "cover";
                img.style.marginRight = "8px";
                img.style.borderRadius = "4px";
                item.appendChild(img);
            } else {
                const icon = document.createElement("i");
                icon.className = "bx bx-paperclip me-2";
                item.appendChild(icon);
            }

            // Dosya adı
            const fileNameSpan = document.createElement("span");
            fileNameSpan.textContent = file.name;
            item.appendChild(fileNameSpan);

            // Silme butonu
            const deleteBtn = document.createElement("button");
            deleteBtn.innerHTML = "&times;";
            deleteBtn.classList.add("btn", "btn-sm", "btn-link", "text-danger", "ms-auto");
            deleteBtn.style.fontSize = "20px";
            deleteBtn.addEventListener("click", () => {
                files.splice(index, 1);

                // input içindeki FileList'i güncelle
                const dt = new DataTransfer();
                files.forEach(f => dt.items.add(f));
                fileInput.files = dt.files;

                // Önizlemeyi yeniden çiz
                fileInput.dispatchEvent(new Event("change"));
            });
            item.appendChild(deleteBtn);

            list.appendChild(item);
        });

        preview.appendChild(list);
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
