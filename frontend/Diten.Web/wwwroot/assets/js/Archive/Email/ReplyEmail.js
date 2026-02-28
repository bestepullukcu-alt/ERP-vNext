document.addEventListener('DOMContentLoaded', function () {


    const replyButton = document.querySelector(".reply-email-send");
    if (!replyButton) {
        console.error("replyButton butonu DOM'da bulunamadı.");
        return;
    }

    replyButton.addEventListener("click", function (e) {
        e.preventDefault();
        sendEmail();
    });

    setupFileAttachmentPreview();


});


async function replyEmail(EmailId) {

    const replyEmail = getEmailById(EmailId);
    const replyFrom = (replyEmail.fromEmail && replyEmail.fromEmail.trim()) !== '' ? replyEmail.fromEmail : '';
    const userId = window.getUserId();






    const emailForm = document.querySelector(".email-compose-form");


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

    const url = `${window.ApiBaseUrl}/api/PvUser/Email/send-email`;

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
