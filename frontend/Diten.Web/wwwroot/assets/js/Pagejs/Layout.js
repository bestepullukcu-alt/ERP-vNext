'use strict'
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


    restoreOpenedMenu();
    setupMenuClickHandler();
});
document.addEventListener("click", function (e) {
    const link = e.target.closest("a.menu-link");
    if (!link) return;

    const parentMenuItem = link.closest("li.menu-item");
    const isTopLevel = parentMenuItem?.parentElement?.id === "main-menu";

    if (isTopLevel) {
        const allMenuItems = document.querySelectorAll("ul#main-menu > li.menu-item");

        allMenuItems.forEach(item => {
            item.classList.remove("open", "active");
            const submenu = item.querySelector(".menu-sub");
            if (submenu) submenu.style.display = "none";
        });

        parentMenuItem.classList.add("open", "active");
        const submenu = parentMenuItem.querySelector(".menu-sub");
        if (submenu) submenu.style.display = "block";

        // localStorage güncelle
        const labelDiv = parentMenuItem.querySelector("a.menu-link > div");
        if (labelDiv) {
            const openedMenu = labelDiv.textContent.trim();
            localStorage.setItem("openedMenu", openedMenu);
        }
    }
});
async function loadShortcuts() {
    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/PvTenant/TenantApplications/GetApplicationsByTenantId`);
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
                .filter(menu => menu.menuName !== "Countries" && menu.menuName !== "Country") // BUILT-IN FILTER: Hide Countries module
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
    const user = window.CurrentUser;
    if (!user) {
        window.location.href = "/account/login";
        return;
    }
    const postData = {
        tenantId: "", // Gerekirse doldur
        applicationId: "", // Gerekirse doldur
        roleIds: Array.isArray(user.roles) ? user.roles : []
    };

    try {
        const response = await fetch(`${window.ApiBaseUrl}/services/PvTenant/Role/GetPermissionByRoleIds`, {
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



function restoreOpenedMenu() {
    const openedMenu = localStorage.getItem("openedMenu");
    const menuItems = document.querySelectorAll("#main-menu > li.menu-item");

    menuItems.forEach(item => {
        const labelDiv = item.querySelector("a.menu-link > div");
        const submenu = item.querySelector(".menu-sub");
        const arrow = item.querySelector(".menu-arrow");

        if (labelDiv?.textContent.trim().includes(openedMenu)) {
            item.classList.add("open", "active");
            if (submenu) submenu.style.display = "block";
            if (arrow) arrow.textContent = "▼"; // aşağı ok
        } else {
            item.classList.remove("open", "active");
            if (submenu) submenu.style.display = "none";
            if (arrow) arrow.textContent = ">"; // sağ ok
        }
    });
}

function setupMenuClickHandler() {
    const menuItems = document.querySelectorAll("#main-menu > li.menu-item > a.menu-link");

    menuItems.forEach(link => {
        link.addEventListener("click", function (e) {
            e.preventDefault();

            const clickedItem = this.closest("li.menu-item");
            const labelDiv = this.querySelector("div");
            const submenu = clickedItem.querySelector(".menu-sub");
            const arrow = clickedItem.querySelector(".menu-arrow");
            const label = labelDiv?.textContent.trim();

            const isAlreadyOpen = clickedItem.classList.contains("open");

            // Tüm menüleri kapat
            document.querySelectorAll("#main-menu > li.menu-item").forEach(item => {
                item.classList.remove("open", "active");
                const sm = item.querySelector(".menu-sub");
                const ar = item.querySelector(".menu-arrow");
                if (sm) sm.style.display = "none";
                if (ar) ar.textContent = ">";
            });

            if (!isAlreadyOpen) {
                clickedItem.classList.add("open", "active");
                if (submenu) submenu.style.display = "block";
                if (arrow) arrow.textContent = "▼";
                if (label) localStorage.setItem("openedMenu", label);
            } else {
                // Aynı menüyü tekrar tıklarsa kapat ve localStorage temizle
                if (label) localStorage.removeItem("openedMenu");
            }
        });
    });
}





function highlightActiveSubmenu() {
    const path = window.location.pathname;

    const links = document.querySelectorAll("ul#main-menu a.menu-link");

    links.forEach(link => {
        if (link.href.includes(path)) {
            const li = link.closest("li");
            li?.classList.add("active");

            const parent = link.closest("li.menu-item");
            parent?.classList.add("open", "active");
        }
    });
}



function getClaimFromToken(claimKey) {
    const user = window.CurrentUser || null;
    if (!user) return null;

    switch (claimKey) {
        case "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name":
            return [user.firstName, user.lastName].filter(Boolean).join(" ").trim() || null;
        case "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier":
            return user.id || null;
        case "http://schemas.microsoft.com/ws/2008/06/identity/claims/role":
            return user.roles || [];
        default:
            return null;
    }
}

function getUserName() {
    return getClaimFromToken("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
}

function getUserId() {
    const user = window.CurrentUser || {};
    return user.id || null;
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
                autoSelectIfSingle: !isChildMultiple && config.autoSelectIfSingle
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
    const isSelectPicker = hasJquery && $(select).hasClass('selectpicker'); // bootstrap-select

    const config = {
        valueKey: options.valueKey || 'id',
        textKey: options.textKey || 'name',
        placeholder: options.placeholder || 'Select...',
        autoSelectIfSingle: options.autoSelectIfSingle !== false
    };

    // Helper to disable/enable uniformly
    const disableSelect = (state) => {
        if (isSelect2 || isSelectPicker) {
            $(select).prop('disabled', state);
            // refresh plugin so UI updates
            if (isSelect2) $(select).trigger('change.select2');
            if (isSelectPicker) $(select).selectpicker('refresh');
        } else {
            select.disabled = state;
        }
    };

    // Setup placeholder for selectpicker: use title attribute for bootstrap-select
    if (isSelectPicker && !isMultiple) {

        select.setAttribute('title', config.placeholder);
    }



    if (isSelectPicker && isMultiple) {
        // For multiple, bootstrap-select shows noneSelectedText option — set data attribute
        select.setAttribute('data-none-selected-text', config.placeholder);
    }
    if ($(select).data('selectpicker')) {
        $(select).selectpicker('destroy');  // 🧨 tüm eski UI, cache, seçenekler sıfırlanır
    }
    disableSelect(true);
    select.innerHTML = `<option value="">Loading...</option>`;

    select.innerHTML = `<option value="">Loading...</option>`;
    select.disabled = true;

    if (isSelectPicker) {
        select.setAttribute(isMultiple ? "data-none-selected-text" : "title", config.placeholder);
    }

    let list = [];

    try {
        // load list from options.data or options.apiUrl
        if (Array.isArray(options.data)) {
            list = options.data;
        } else if (options.apiUrl) {
            const response = await fetch(options.apiUrl);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const result = await response.json();
            list = Array.isArray(result) ? result : (result.data || result.result || []);
        }

        if (!Array.isArray(list) || list.length === 0) {
            // no data
            select.innerHTML = `<option value="">No data found</option>`;
            disableSelect(false);
            return;
        }

        // Build options. For multiple, don't add empty placeholder option (bootstrap-select handles title/noneSelectedText)
        select.innerHTML = isMultiple ? '' : `<option value="">${config.placeholder}</option>`;

        list.forEach(item => {
            const value = item[config.valueKey];
            const text = item[config.textKey];

            const option = document.createElement('option');
            option.value = value;
            option.textContent = text;
            select.appendChild(option);
        });

        // If plugin present, refresh to render new options
        if (isSelect2) {
            // select2 usually requires re-init or refresh; trigger change is usually fine
            $(select).trigger('change.select2');
        }
        if (isSelectPicker) {
            // refresh so bootstrap-select rebuilds the dropdown
            // make sure selectpicker() has been initialized before calling refresh
            // if not initialized, initializing here is harmless
            if (typeof $(select).selectpicker === 'function') {
                $(select).selectpicker('refresh');
            }
        }

        // Handle selected values
        if (options.selectedValue !== undefined && options.selectedValue !== null) {
            // Normalize to array of strings for comparison
            let valuesToSelect = [];
            if (isMultiple) {
                valuesToSelect = Array.isArray(options.selectedValue)
                    ? options.selectedValue.map(String)
                    : [String(options.selectedValue)];
            } else {
                valuesToSelect = [String(options.selectedValue)];
            }

            if (isSelect2) {
                // select2 wants array for multiple, single value for single
                const val = isMultiple ? valuesToSelect : valuesToSelect[0];
                $(select).val(val).trigger('change.select2');
            } else if (isSelectPicker) {
                // bootstrap-select supports 'val' method
                const val = isMultiple ? valuesToSelect : valuesToSelect[0];
                if (typeof $(select).selectpicker === 'function') {
                    $(select).selectpicker('val', val);
                    $(select).selectpicker('refresh');
                } else {
                    // fallback to plain DOM selection
                    Array.from(select.options).forEach(opt => {
                        opt.selected = valuesToSelect.includes(opt.value);
                    });
                    select.dispatchEvent(new Event('change'));
                }
            } else {
                // plain select
                if (isMultiple) {
                    Array.from(select.options).forEach(opt => {
                        opt.selected = valuesToSelect.includes(opt.value);
                    });
                } else {
                    select.value = valuesToSelect[0] ?? '';
                }
                select.dispatchEvent(new Event('change'));
            }
        } else if (!isMultiple && list.length === 1 && config.autoSelectIfSingle) {
            // auto select single item (only for single selects)
            const onlyItemValue = String(list[0][config.valueKey] ?? '');
            if (isSelect2) {
                $(select).val(onlyItemValue).trigger('change.select2');
            } else if (isSelectPicker && typeof $(select).selectpicker === 'function') {
                $(select).selectpicker('val', onlyItemValue);
                $(select).selectpicker('refresh');
            } else {
                select.value = onlyItemValue;
                select.dispatchEvent(new Event('change'));
            }
        }

    } catch (error) {
        console.error(`populateSelect hata (${selectId}):`, error);
        select.innerHTML = `<option value="">Error loading data</option>`;
    } finally {
        disableSelect(false);
    }
};




