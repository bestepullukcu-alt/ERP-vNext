'use strict';

document.addEventListener('DOMContentLoaded', function () {


    const googleBtn = document.getElementById('btnGoogleLogin');
    if (!googleBtn) {
        console.error("btnGoogleLogin butonu DOM'da bulunamadı.");
        return;
    }
    googleBtn.addEventListener('click', handleGoogleLogin);

    fetchEmails("inbox");

    const emailContacts = document.getElementById('emailContacts');
    if (!emailContacts) {
        console.error("emailContacts select i DOM'da bulunamadı.");
        return;
    }
    else {

        const assetsPath = "/assets/"; // avatar resmi varsa burada tanımlanır

        loadUsersAndInitSelect2("emailContacts", assetsPath);

    }


});

let emailId = "";

$(document).on('click', '.email-list-item', function () {
    alert('çalıştı!');

    emailId = $(this).data('id');
    const email = getEmailById(emailId);

    const emailFrom = (email.from && email.from.trim() !== '' ? email.from : email.fromEmail) || '';



    const $target = $($(this).data('target'));
    if ($target.length) {
        $target.addClass('show');
    }

    $('.email-detail-name').text(getSenderName(emailFrom));
    $('.email-detail-email').text(getSenderEmail(emailFrom));
    $('.email-detail-date').text(formatDateTime(email.receivedDate));
    $('.email-detail-body').html(email.body);
    $('.email-detail-attachment').text(email.attachmentFileName || '');
    document.getElementById('emailDetailAvatar').textContent = getInitials(emailFrom);
    document.querySelector('.card-header.border-0.fw-normal.pb-4').textContent = `Reply to ${emailFrom || 'Unknown Sender'}`;

    const previousMessagesCount = email.previousMails ? email.previousMails.length : 0;
    const earlierMessagesCountElem = document.getElementById('earlierMessagesCount');
    if (earlierMessagesCountElem) {
        if (previousMessagesCount > 0) {
            earlierMessagesCountElem.textContent = `${previousMessagesCount} Earlier Message${previousMessagesCount !== 1 ? 's' : ''}`;
            earlierMessagesCountElem.style.display = 'block';  // Göster
        } else {
            earlierMessagesCountElem.style.display = 'none';   // Gizle
        }
    }


});



$(document).on('click', '#earlierMessagesCount', function () {

    const email = getEmailById(emailId);
    console.log(email);

    renderPreviousMails(email.previousMails);

    const emailCardsPrev = document.querySelectorAll('.email-card-prev');

    // Örneğin tüm kartların görünürlüğünü toggle yapmak için:
    emailCardsPrev.forEach(card => {
        if (card.style.display === 'none' || !card.style.display) {
            card.style.display = 'block';
            card.classList.add('slide-toggle');
        } else {
            card.classList.remove('slide-toggle');
            setTimeout(() => card.style.display = 'none', 300);
        }
    });

});



function renderPreviousMails(previousMails) {
    const earlierMessagesElem = document.getElementById('earlierMessagesCount');

    if (!earlierMessagesElem) return;

    previousMails.forEach(mail => {
        // Ana kart div'i
        const mailCard = document.createElement('div');
        mailCard.className = 'card email-card-prev mx-sm-6 mx-3 mb-4';

        // Kart header
        mailCard.innerHTML = `
            <div class="card-header d-flex justify-content-between align-items-center flex-wrap border-bottom">
                <div class="d-flex align-items-center mb-sm-0 mb-3">
                    <img src="../../assets/img/avatars/2.png" alt="user-avatar"
                        class="flex-shrink-0 rounded-circle me-4" height="38" width="38">
                    <div class="flex-grow-1 ms-1">
                        <h6 class="m-0 fw-normal">${mail.from || ''}</h6>
                        <small class="text-body">${mail.fromEmail || ''}</small>
                    </div>
                </div>
                <div class="d-flex align-items-center">
                    <p class="mb-0 me-4 text-body-secondary">
                        ${mail.receivedDate ? new Date(mail.receivedDate).toLocaleString() : ''}
                    </p>
                    <span class="btn btn-icon">
                        <i class="icon-base bx bx-paperclip icon-md cursor-pointer"></i>
                    </span>
                    <span class="btn btn-icon">
                        <i class="email-list-item-bookmark icon-base bx bx-star icon-md cursor-pointer"></i>
                    </span>
                    <div class="dropdown">
                        <button class="btn btn-icon p-0" type="button" data-bs-toggle="dropdown" aria-haspopup="true" aria-expanded="false">
                            <i class="icon-base bx bx-dots-vertical icon-md"></i>
                        </button>
                        <div class="dropdown-menu dropdown-menu-end">
                            <a class="dropdown-item scroll-to-reply" href="javascript:void(0)">
                                <i class="icon-base bx bx-share me-1"></i>
                                <span class="align-middle">Reply</span>
                            </a>
                            <a class="dropdown-item" href="javascript:void(0)">
                                <i class="icon-base bx bx-share me-1"></i>
                                <span class="align-middle">Forward</span>
                            </a>
                            <a class="dropdown-item" href="javascript:void(0)">
                                <i class="icon-base bx bx-info-circle me-1"></i>
                                <span class="align-middle">Report</span>
                            </a>
                        </div>
                    </div>
                </div>
            </div>
            <div class="card-body pt-6">
                <p class="fw-medium">${mail.subject || ''}</p>
                <div>${mail.body || ''}</div>
                
            </div>
        `;

        // earlierMessagesCount'un altına ekle
        earlierMessagesElem.insertAdjacentElement('afterend', mailCard);
    });
}






async function handleGoogleLogin() {
    alert('Google ile giriş başlatılıyor...');


    const userId = window.getUserId();


    const url = `${window.ApiBaseUrl}/api/PvUser/Auth/login?userId=${encodeURIComponent(userId)}`;

    fetch(url)
        .then(response => response.json())
        .then(data => {
            window.location.href = data.url;  // Google OAuth sayfasına yönlendir
        })
        .catch(error => {
            console.error(error);
            alert('Google ile giriş yapılamıyor.');
        });
}

function fetchEmails(Folder) {

    const userId = window.getUserId();


    fetch(`${window.ApiBaseUrl}/api/PvUser/Email/gmail/user-emails/${userId}?Folder=${Folder}`, {
        headers: {
            'Authorization': 'Bearer ' + localStorage.getItem('token') // veya nerede tutuyorsan
        }
    }).then(response => response.json())
        .then(emails => {
            renderEmailList(emails);

            const unreadCount = emails.data.filter(email => !email.isRead).length;
            document.getElementById('unreadCountBadge').textContent = unreadCount;


        })
        .catch(error => {
            console.error('Hata:', error);
            $('#emailList').empty().append('<li class="p-3 text-danger">E-postalar yüklenemedi.</li>');
        });
}

// Sayfa yüklendiğinde e-postaları getir

let emailDataCache = [];
function renderEmailList(response) {
    const emails = response.data;
    emailDataCache = response.data;
    const $list = $('#emailList');
    $list.empty();

    if (!emails || emails.length === 0) {
        $('.email-list-empty').removeClass('d-none');
        return;
    } else {
        $('.email-list-empty').addClass('d-none');
    }

    emails.forEach((email, index) => {
        const rawFrom = (email.from && email.from.trim() !== '' ? email.from : email.fromEmail) || '';
        const senderName = rawFrom.includes('<') ? rawFrom.split('<')[0].trim() : rawFrom.trim();


        const senderInitial = getInitials(senderName);

        const date = new Date(email.receivedDate);
        const time = date.toLocaleTimeString('tr-TR', {
            hour: '2-digit',
            minute: '2-digit'
        });
        const dateStr = date.toLocaleDateString('tr-TR', {
            day: '2-digit',
            month: 'short',
            year: 'numeric'
        });

        const subject = email.subject || '(Konu yok)';
        const checkboxId = `email-${index}`;

        const listItem = `
  <li class="email-list-item email-marked-read d-flex align-items-center"
      data-id="${email.id}"
      data-starred="false"
      data-bs-toggle="sidebar"
      data-target="#app-email-view">
    <div class="d-flex align-items-center w-100">
      <div class="form-check mb-0 ms-2">
        <input class="email-list-item-input form-check-input" type="checkbox" id="${checkboxId}" />
        <label class="form-check-label" for="${checkboxId}"></label>
      </div>
      <span class="ms-sm-3 me-4 d-sm-inline-block d-none">
        <i class="email-list-item-bookmark icon-base bx bx-star icon-md cursor-pointer ms-1"></i>
      </span>
      <div class="avatar bg-primary text-white rounded-circle d-flex align-items-center justify-content-center flex-shrink-0 me-sm-2 me-0"
           style="width: 32px; height: 32px;">
        <span class="fw-bold">${senderInitial}</span>
      </div>
      <div class="email-list-item-content ms-2 ms-sm-0 me-2">
        <span class="email-list-item-username me-2 text-heading">${senderName}</span>
        <small class="email-list-item-subject d-xl-inline-block d-block">
          ${subject}
        </small>
      </div>
      <div class="email-list-item-meta ms-auto d-flex align-items-center">
        <span class="email-list-item-label badge badge-dot bg-danger d-none d-md-inline-block me-2"
              data-label="private"></span>
        <small class="email-list-item-time text-body-secondary">${dateStr}</small>
        <ul class="list-inline email-list-item-actions">
          <li class="list-inline-item email-delete btn btn-icon">
            <i class="icon-base bx bx-trash icon-md"></i>
          </li>
          <li class="list-inline-item email-unread btn btn-icon">
            <i class="icon-base bx bx-envelope icon-md"></i>
          </li>
          <li class="list-inline-item btn btn-icon">
            <i class="icon-base bx bx-info-circle icon-md"></i>
          </li>
        </ul>
      </div>
    </div>
  </li>
  `;

        $list.append(listItem);
    });

}

// Ad soyad varsa baş harfleri al
function getInitials(fullName) {



    if (!fullName || typeof fullName !== 'string') return '?';

    const cleanName = fullName.split('(')[0].trim(); // örn. "John Doe (via Google Chat)" → "John Doe"
    const parts = cleanName.split(' ').filter(x => x.trim() !== '');

    if (parts.length >= 2) {
        return (parts[0][0] + parts[1][0]).toUpperCase();
    } else if (parts.length === 1) {
        return parts[0].substring(0, 2).toUpperCase();
    } else {
        // Boşluk yoksa e-posta gibi tek parça olabilir → ilk iki harfi al
        return cleanName.substring(0, 2).toUpperCase();
    }
}


async function loadUsersAndInitSelect2(selectId, assetsPath) {
    const userId = window.getUserId();

    const apiUrl = `${window.ApiBaseUrl}/api/PvUser/User/GetUsersByUserIdAndCompanyId/${userId}`;
    const $select = $(`#${selectId}`);

    try {
        const response = await fetch(apiUrl);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        const users = Array.isArray(result) ? result : result.data;

        if (!Array.isArray(users)) {
            throw new Error("User data is not in the expected format.");
        }

        $select.empty(); // varsa eski <option>’ları sil

        users.forEach(user => {
            const option = new Option(user.email, user.email, false, false);
            $(option).attr('data-avatar', ''); // avatar olmadığı için boş bırakıyoruz
            $select.append(option);
        });

        initSelect2($select, assetsPath); // Select2’yi başlat

    } catch (err) {
        console.error("Failed to load user list:", err);
    }
}

function initSelect2($element, assetsPath) {
    if ($element.length) {
        $element
            .wrap('<div class="position-relative"></div>')
            .select2({
                placeholder: 'Select value',
                dropdownParent: $element.parent(),
                closeOnSelect: false,
                templateResult: renderContactsAvatar,
                templateSelection: renderContactsAvatar,
                escapeMarkup: m => m
            });
    }
}
function renderContactsAvatar(option) {

    //if (!option.id) return option.text;

    const avatar = $(option.element).data('avatar');
    const email = $(option.element).data('email');

    const initials = option.text
        .split(' ')
        .map(w => w[0])
        .join('')
        .toUpperCase();

    const $avatar = `
        <div class="d-flex align-items-center">
          <div class="avatar avatar-xs me-2 w-px-28 h-px-28 bg-label-primary rounded-circle d-flex align-items-center justify-content-center fw-medium text-white bg-label-info">
            ${avatar ? `<img src="${assetsPath}/img/avatars/${avatar}" class="rounded-circle" alt="avatar"/>` : initials}
          </div>
          <div class="d-flex flex-column">
            <div class="fw-medium">${option.text}</div>
            ${email ? `<small class="text-muted">${email}</small>` : ''}
          </div>
        </div>`;

    return $avatar;
}



function getEmailById(id) {
    return emailDataCache.find(e => e.id === id);
}

function getSenderName(from) {
    return from?.split('<')[0]?.trim() || 'Gönderen';
}

function getSenderEmail(from) {
    const match = from?.match(/<(.+)>/);
    return match ? match[1] : '';
}

function formatDateTime(dateStr) {
    const date = new Date(dateStr);
    return date.toLocaleString('tr-TR', {
        day: '2-digit',
        month: 'long',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

