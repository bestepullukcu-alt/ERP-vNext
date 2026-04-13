'use strict'
const prhttp = window.location.protocol;
const domain = window.location.hostname;
const port = prhttp === 'https:' ? '5003' : '5000';

/**
 * Initialize a character counter for any input or textarea.
 * @param {string} inputId - The ID of the input or textarea element.
 * @param {number} maxChars - Maximum number of characters allowed.
 */

document.addEventListener('DOMContentLoaded', function () {
    loadShortcuts();
    const menuData = localStorage.getItem("menuData");

    if (menuData) {
        // Daha önce yüklendiyse localStorage'dan yeniden çiz
        loadMenu(JSON.parse(menuData));
    } else {
        // İlk kez login olduysa API'den çek ve kaydet
        getMenuAndLoad();
    }
});
// Event listener removed
async function loadShortcuts() {
    try {
        const response = await fetch(`${prhttp}//${domain}:${port}/services/PvTenant/TenantApplications/GetApplicationsByTenantId`);
        const data = await response.json();

        const container = document.getElementById("shortcuts-container");
        container.innerHTML = ""; // önce boşalt

        const apps = data.data;

        // Grupları 2’li satırlara ayır (her satır 2 öğe)
        for (let i = 0; i < apps.length; i += 2) {
            const row = document.createElement("div");
            row.className = "row row-bordered overflow-visible g-0";

            for (let j = i; j < i + 2 && j < apps.length; j++) {
                const item = apps[j];
                row.innerHTML += `
            <div class="dropdown-shortcuts-item col">
              <span class="dropdown-shortcuts-icon rounded-circle mb-3">
                <i class="icon-base bx bx-${item.applicationIcon} icon-26px text-heading"></i>
              </span>
              <a href="#" class="stretched-link">${item.applicationName}</a>
              <small>${item.applicationName}</small>
            </div>`;
            }

            container.appendChild(row);
        }

    } catch (err) {
        console.error("Shortcuts yüklenemedi:", err);
    }
}

async function loadMenu(response) {
    // API'ye gönderilecek POST verisi

    try {



        const menuContainer = document.getElementById('main-menu');
        menuContainer.innerHTML = "";

        response.data.forEach(header => {
            // Menü başlığı
            const headerLi = document.createElement("li");
            headerLi.classList.add("menu-header", "small");
            headerLi.innerHTML = `
        <span class="menu-header-text">${header.menuHeaderName}</span>
      `;
            menuContainer.appendChild(headerLi);
            header.menus
                .sort((a, b) => a.order - b.order)
                .forEach(menu => {
                    // Ana menü seviyesi (altında sayfalar varsa toggle'lı olacak)
                    const menuItem = document.createElement("li");
                    menuItem.classList.add("menu-item");

                    const hasPages = menu.pages && menu.pages.length > 0;
                    const iconClass = menu.icon || "bx bx-folder"; // varsayılan ikon

                    if (hasPages) {
                        menuItem.innerHTML = `
              <a href="javascript:void(0);" class="menu-link menu-toggle">
                <i class="menu-icon icon-base ${iconClass}"></i>
                <div>${menu.menuName}</div>
              </a>
              <ul class="menu-sub"></ul>
            `;

                        const submenu = menuItem.querySelector(".menu-sub");

                        menu.pages
                            .sort((a, b) => a.order - b.order)
                            .forEach(page => {
                                const pageItem = document.createElement("li");
                                pageItem.classList.add("menu-item");
                                pageItem.innerHTML = `
                  <a href="/${page.url}" class="menu-link">
                    <div>${page.pageName}</div>
                  </a>
                `;
                                submenu.appendChild(pageItem);
                            });
                    } else {
                        // Eğer alt sayfa yoksa direkt link olarak göster
                        menuItem.innerHTML = `
              <a href="${menu.url || '#'}" class="menu-link">
                <i class="menu-icon icon-base ${iconClass}"></i>
                <div data-menu-id="${menu.menuId}">${menu.menuName}</div>
              </a>
            `;
                    }

                    menuContainer.appendChild(menuItem);


                });

        });

        // Initialize Events & State after DOM is ready
        // Initialize Events & State after DOM is ready
        wireMenuEvents();
        initSidebarObserver();

        // Initial state logic
        if (isMenuCollapsed()) {
            onSidebarCollapse();
        } else {
            applyActiveMenuFromCurrentUrl();
        }

    } catch (error) {
        console.error('Bir hata oluştu:', error);
    }
}

function loadMenuOnce(response) {
    const menuData = localStorage.getItem("menuData");

    if (!menuData) {
        loadMenu(response); // asıl menü oluşturma fonksiyonun
        localStorage.setItem("menuData", JSON.stringify(response)); // veriyi sakla
        localStorage.setItem("menuLoaded", "true");
    }
    else {
        loadMenu(JSON.parse(menuData)); // localStorage'dan yükle
    }
}


async function getMenuAndLoad() {

    const token = localStorage.getItem("token");
    if (!token) {
        window.location.href = "/account/login";
    }

    const decodedToken = decodeJWT(token);  // Burada token decode ediliyor
    const roleClaimKey = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    let roleIds = decodedToken[roleClaimKey];
    if (typeof roleIds === "string") {
        roleIds = [roleIds]; // Tek bir rol varsa array'e çevir
    }
    const postData = {
        tenantId: "", // Gerekirse doldur
        applicationId: "", // Gerekirse doldur
        roleIds: roleIds
    };

    try {
        const response = await fetch(`${prhttp}//${domain}:${port}/services/PvTenant/Role/GetPermissionByRoleIds`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                // Eğer token gerekiyorsa:
                // 'Authorization': `Bearer ${yourToken}`
            },
            body: JSON.stringify(postData)
        });

        const data = await response.json();

        // Menüyi yükle
        loadMenuOnce(data);

    } catch (error) {
        console.error('Menü alınırken hata oluştu:', error);
    }
}



/**
 * Menü Logic Refactor
 * URL-based active state & Single event delegation
 */



function isMenuCollapsed() {
    return document.body.classList.contains("layout-menu-collapsed")
        || document.documentElement.classList.contains("layout-menu-collapsed")
        || document.querySelector(".layout-menu")?.classList.contains("collapsed")
        || document.querySelector(".layout-menu")?.classList.contains("layout-menu-collapsed");
}

function onSidebarCollapse() {
    // Aggressively close all submenus visually
    // BUT maintain 'active' state logic for later restoration
    document.querySelectorAll("#main-menu .menu-item.open").forEach(li => {
        // Remove open class
        li.classList.remove("open");
    });

    document.querySelectorAll("#main-menu .menu-sub").forEach(sub => {
        sub.style.display = "none";
    });
}

function initSidebarObserver() {
    // Watch body, html, and .layout-menu for class changes
    const targets = [
        document.body,
        document.documentElement,
        document.querySelector(".layout-menu")
    ].filter(el => el !== null);

    const config = { attributes: true, attributeFilter: ['class'] };

    const callback = (mutationsList) => {
        for (const mutation of mutationsList) {
            if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                if (isMenuCollapsed()) {
                    onSidebarCollapse();
                } else {
                    applyActiveMenuFromCurrentUrl();
                }
            }
        }
    };

    const observer = new MutationObserver(callback);
    targets.forEach(target => observer.observe(target, config));
}

function setSubmenuOpen(liItem, isOpen) {
    if (!liItem) return;
    const submenu = liItem.querySelector(".menu-sub");

    // Decoupled: Open/Close only visual display + open class.
    // Active state is managed STRICTLY by applyActiveMenuFromCurrentUrl
    if (isOpen) {
        liItem.classList.add("open");
        if (submenu) {
            submenu.style.display = "block";
        }
    } else {
        liItem.classList.remove("open");
        if (submenu) {
            submenu.style.display = "none";
        }
    }
}

function applyActiveMenuFromCurrentUrl() {
    // CLEANUP FIRST: Remove all active/open states to avoid conflicts
    const menuContainer = document.getElementById("main-menu");
    if (!menuContainer) return;

    menuContainer.querySelectorAll(".menu-item.active").forEach(el => el.classList.remove("active"));
    menuContainer.querySelectorAll(".menu-item.open").forEach(el => el.classList.remove("open"));
    menuContainer.querySelectorAll(".menu-sub").forEach(el => el.style.display = "none");

    const currentPath = window.location.pathname.toLowerCase();
    const allLinks = document.querySelectorAll("#main-menu a.menu-link[href]");
    let found = false;

    // 1. Try exact URL match
    allLinks.forEach(link => {
        if (found) return;
        try {
            const linkPath = new URL(link.href, window.location.origin).pathname.toLowerCase();

            if (linkPath === currentPath) {
                found = true;
                const li = link.closest("li.menu-item");
                if (li) li.classList.add("active");

                const parentUl = li.closest("ul.menu-sub");
                if (parentUl) {
                    const parentLi = parentUl.closest("li.menu-item");
                    // Open parent but strictly one level up if that's the structure
                    setSubmenuOpen(parentLi, true);
                }
            }
        } catch (e) { }
    });

    // 2. Fallback to localStorage if no URL match
    if (!found) {
        const storedPath = localStorage.getItem("activeMenuPath");
        if (storedPath) {
            allLinks.forEach(link => {
                if (found) return;
                try {
                    const linkPath = new URL(link.href, window.location.origin).pathname.toLowerCase();
                    if (linkPath === storedPath.toLowerCase()) {
                        found = true;
                        const li = link.closest("li.menu-item");
                        if (li) li.classList.add("active");

                        const parentUl = li.closest("ul.menu-sub");
                        if (parentUl) {
                            const parentLi = parentUl.closest("li.menu-item");
                            setSubmenuOpen(parentLi, true);
                        }
                    }
                } catch (e) { }
            });
        }
    }
}

function wireMenuEvents() {
    const menuContainer = document.getElementById("main-menu");
    if (!menuContainer) return;

    // Remove old listeners if any (though we removed the code blocks)
    // Clone node trick could clean events but might break other things.
    // Since we removed explicit listeners, simple addEventListener is fine.

    // Prevent double binding check
    if (menuContainer.dataset.eventsWired === "true") return;
    menuContainer.dataset.eventsWired = "true";

    menuContainer.addEventListener("click", (e) => {
        const toggleLink = e.target.closest(".menu-toggle");
        const subLink = e.target.closest("a.menu-link:not(.menu-toggle)");

        // A) Top-level Toggle Click
        if (toggleLink) {
            e.preventDefault();
            const parentLi = toggleLink.closest("li.menu-item");
            const isAlreadyOpen = parentLi.classList.contains("open");

            // Close all others
            const allTopLevels = menuContainer.querySelectorAll("li.menu-item");
            allTopLevels.forEach(item => {
                // check if it has a submenu, otherwise it's a direct link
                if (item.querySelector(".menu-sub")) {
                    setSubmenuOpen(item, false);
                }
            });

            // Toggle current
            if (!isAlreadyOpen) {
                setSubmenuOpen(parentLi, true);
            }
            return;
        }

        // B) Submenu Link Click
        if (subLink) {
            // Save state
            try {
                // Ensure we get a valid path even if href is full URL
                const path = new URL(subLink.href, window.location.origin).pathname;
                localStorage.setItem("activeMenuPath", path);
            } catch (e) { }
            // Allow default navigation
        }
    });
}



function decodeJWT(token) {

    const base64Url = token.split('.')[1];  // Token'ın ikinci kısmı payload'dır
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');  // Base64 formatını düzelt
    const jsonPayload = decodeURIComponent(atob(base64).split('').map(function (c) {
        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
    }).join(''));
    return JSON.parse(jsonPayload);
}

function getClaimFromToken(claimKey) {
    const token = localStorage.getItem("token");
    if (!token) return null;


    const decoded = decodeJWT(token);
    return decoded ? decoded[claimKey] : null;
}

function getUserName() {
    return getClaimFromToken("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
}

function getUserId() {
    const token = localStorage.getItem("token");
    if (!token) return null;

    const decoded = decodeJWT(token);
    return decoded
        ? decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]
        : null;
}
function getUserRoleId() {
    return getClaimFromToken("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
}

window.initializeCharacterCounter = function (inputId, maxChars) {
    var input = document.getElementById(inputId);
    if (!input) return;

    var parent = input.parentNode;
    parent.style.position = 'relative';

    var counter = document.createElement('div');
    counter.className = 'text-muted small mt-1';
    counter.style.position = 'absolute';
    counter.style.right = '0';
    counter.style.bottom = '-20px';
    counter.innerText = maxChars + ' characters remaining';
    parent.appendChild(counter);

    input.addEventListener('input', function () {
        if (input.value.length > maxChars) {
            input.value = input.value.substring(0, maxChars);
        }
        var remaining = maxChars - input.value.length;
        counter.innerText = remaining + ' characters remaining';

        if (remaining <= 100 && remaining > 0) {
            counter.style.color = 'orange';
        } else if (remaining <= 0) {
            counter.style.color = 'red';
        } else {
            counter.style.color = '';
        }
    });

    if (input.form) {
        input.form.addEventListener('reset', function () {
            setTimeout(function () {
                counter.innerText = maxChars + ' characters remaining';
                counter.style.color = '';
            }, 0);
        });
    }
};




window.getUserName = getUserName;
window.getUserId = getUserId;
window.decodeJWT = decodeJWT;
window.getClaimFromToken = getClaimFromToken;
window.getUserRoleId = getUserRoleId;

/**
 * Global select doldurma fonksiyonu (tam özellikli sürüm)
 * API veya dummy data ile çalışır, loading/disabled durumlarını yönetir.
 *
 * @param {string} selectId - Select elementinin id'si (ör. "add-language")
 * @param {object} options - Ayar nesnesi:
 *    {
 *       apiUrl: "/api/languages",         // (isteğe bağlı)
 *       data: [ { id: 1, name: "English" } ], // (isteğe bağlı, dummy data)
 *       selectedValue: "1",               // (isteğe bağlı, update modu için)
 *       valueKey: "id",                   // (opsiyonel, default "id")
 *       textKey: "name",                  // (opsiyonel, default "name")
 *       placeholder: "Please select...",  // (opsiyonel, default "Select...")
 *       autoSelectIfSingle: true          // (opsiyonel, default true)
 *    }
 */
window.bindDependentSelect = function (parentSelectId, childSelectId, options = {}) {
    const parentSelect = document.getElementById(parentSelectId);
    const childSelect = document.getElementById(childSelectId);

    if (!parentSelect || !childSelect) {
        console.warn(`bindDependentSelect: '${parentSelectId}' veya '${childSelectId}' bulunamadı.`);
        return;
    }

    const config = {
        apiUrlBuilder: options.apiUrlBuilder,
        dataMapper: options.dataMapper || ((response) => response.data || response),
        placeholder: options.placeholder || 'Select...',
        valueKey: options.valueKey || 'id',
        textKey: options.textKey || 'name',
        autoSelectIfSingle: options.autoSelectIfSingle !== false,
        onBeforeLoad: options.onBeforeLoad || null,
        onLoaded: options.onLoaded || null,
        onError: options.onError || null
    };

    const isParentMultiple = parentSelect.hasAttribute('multiple');
    const isChildMultiple = childSelect.hasAttribute('multiple');

    const resetChildSelect = (placeholderText = config.placeholder) => {
        childSelect.innerHTML = `<option value="">${placeholderText}</option>`;
        if (typeof $ !== 'undefined' && $(childSelect).hasClass('select2')) {
            $(childSelect).val(isChildMultiple ? [] : '').trigger('change.select2');
        }
    };

    const handleParentChange = async (value) => {
        if (!value || (Array.isArray(value) && value.length === 0)) {
            resetChildSelect();
            return;
        }

        if (typeof config.apiUrlBuilder !== 'function') {
            console.error("bindDependentSelect: 'apiUrlBuilder' fonksiyonu gerekli.");
            return;
        }

        const apiUrl = config.apiUrlBuilder(value);
        if (!apiUrl) {
            console.warn("bindDependentSelect: apiUrl boş döndü.");
            return;
        }

        try {
            if (config.onBeforeLoad) config.onBeforeLoad(value);

            await window.populateSelect(childSelectId, {
                apiUrl: apiUrl,
                valueKey: config.valueKey,
                textKey: config.textKey,
                placeholder: config.placeholder,
                autoSelectIfSingle: !isChildMultiple && config.autoSelectIfSingle,
                selectedValue: options.selectedChildValue // 🔥 KRİTİK SATIR
            });


            if (config.onLoaded) config.onLoaded();
        } catch (err) {
            console.error(`bindDependentSelect hata (${childSelectId}):`, err);
            if (config.onError) config.onError(err);
        }
    };

    const getParentValue = () => {
        if (typeof $ !== 'undefined' && $(parentSelect).hasClass('select2')) {
            const val = $(parentSelect).val();
            return isParentMultiple && val ? val : val || '';
        } else {
            if (isParentMultiple) {
                return Array.from(parentSelect.selectedOptions).map(opt => opt.value);
            } else {
                return parentSelect.value;
            }
        }
    };

    parentSelect.addEventListener('change', (e) => {
        handleParentChange(getParentValue());
    });

    if (typeof $ !== 'undefined' && $(parentSelect).hasClass('select2')) {
        $(parentSelect).on('select2:select select2:unselect', () => {
            handleParentChange(getParentValue());
        });

        $(parentSelect).on('select2:clear', () => {
            resetChildSelect();
        });
    }

    // Sayfa yüklenirken selected değer varsa çalıştır
    setTimeout(() => {
        const selectedValue = getParentValue();

        if (!selectedValue || (Array.isArray(selectedValue) && selectedValue.length === 0)) {
            setTimeout(() => {
                const retryValue = getParentValue();
                if (retryValue && ((Array.isArray(retryValue) && retryValue.length > 0) || !Array.isArray(retryValue))) {
                    handleParentChange(retryValue);
                }
            }, 300);
        } else {
            handleParentChange(selectedValue);
        }
    }, 150);
};


window.populateSelect = async function (selectId, options = {}) {
    const select = document.getElementById(selectId);
    if (!select) {
        console.warn(`populateSelect: '${selectId}' id'li element bulunamadı.`);
        return;
    }

    // detect multiple
    const isMultiple = select.multiple === true || select.hasAttribute('multiple');

    // detect plugins
    const hasJquery = typeof $ !== 'undefined';
    const isSelect2 = hasJquery && $(select).hasClass('select2');
    const isSelectPicker = hasJquery && $(select).hasClass('selectpicker');

    const config = {
        valueKey: options.valueKey || 'id',
        textKey: options.textKey || 'name',
        placeholder: options.placeholder || 'Select...',
        autoSelectIfSingle: options.autoSelectIfSingle !== false
    };

    const disableSelect = (state) => {
        if (isSelect2 || isSelectPicker) {
            $(select).prop('disabled', state);
            if (isSelect2) $(select).trigger('change.select2');
            if (isSelectPicker) $(select).selectpicker('refresh');
        } else {
            select.disabled = state;
        }
    };

    if (isSelectPicker && !isMultiple) {
        select.setAttribute('title', config.placeholder);
    }

    if (isSelectPicker && isMultiple) {
        select.setAttribute('data-none-selected-text', config.placeholder);
    }

    if ($(select).data('selectpicker')) {
        $(select).selectpicker('destroy');
    }

    disableSelect(true);
    select.innerHTML = `<option value="">Loading...</option>`;

    let list = [];

    try {
        if (Array.isArray(options.data)) {
            list = options.data;
        } else if (options.apiUrl) {
            const response = await fetch(options.apiUrl);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const result = await response.json();
            list = Array.isArray(result) ? result : (result.data || result.result || []);
        }

        // 🔥 FILTER EKLENDİ (EN ÖNEMLİ YER)
        if (typeof options.filter === 'function') {
            list = list.filter(options.filter);
        }

        if (!Array.isArray(list) || list.length === 0) {
            select.innerHTML = `<option value="">No data found</option>`;
            disableSelect(false);
            return;
        }

        select.innerHTML = isMultiple ? '' : `<option value="">${config.placeholder}</option>`;

        list.forEach(item => {
            const option = document.createElement('option');
            option.value = item[config.valueKey];
            option.textContent = item[config.textKey];
            select.appendChild(option);
        });

        if (isSelect2) {
            $(select).trigger('change.select2');
        }

        if (isSelectPicker && typeof $(select).selectpicker === 'function') {
            $(select).selectpicker('refresh');
        }

        if (options.selectedValue !== undefined && options.selectedValue !== null) {
            const values = isMultiple
                ? [].concat(options.selectedValue).map(String)
                : [String(options.selectedValue)];

            if (isSelect2) {
                $(select).val(isMultiple ? values : values[0]).trigger('change.select2');
            } else if (isSelectPicker) {
                $(select).selectpicker('val', isMultiple ? values : values[0]);
                $(select).selectpicker('refresh');
            } else {
                select.value = values[0];
                select.dispatchEvent(new Event('change'));
            }
        } else if (!isMultiple && list.length === 1 && config.autoSelectIfSingle) {
            const val = String(list[0][config.valueKey]);
            select.value = val;
            select.dispatchEvent(new Event('change'));
        }

    } catch (error) {
        console.error(`populateSelect hata (${selectId}):`, error);
        select.innerHTML = `<option value="">Error loading data</option>`;
    } finally {
        disableSelect(false);
    }
};





