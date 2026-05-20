/**
 * Main
 */

'use strict';

window.isRtl = window.Helpers.isRtl();
window.isDarkStyle = window.Helpers.isDarkStyle();
let menu,
  animate;
document.addEventListener('DOMContentLoaded', function () {
  // class for ios specific styles
  if (navigator.userAgent.match(/iPhone|iPad|iPod/i)) {
    document.body.classList.add('ios');
  }
});

(function () {
  // Initialize menu
  //-----------------

  let layoutMenuEl = document.querySelectorAll('#layout-menu');
  layoutMenuEl.forEach(function (element) {
    menu = new Menu(element, {
      orientation: 'vertical',
      closeChildren: false
    });
    // Change parameter to true if you want scroll animation
    window.Helpers.scrollToActive((animate = false));
    window.Helpers.mainMenu = menu;
  });

  // Initialize menu togglers and bind click on each
  let menuToggler = document.querySelectorAll('.layout-menu-toggle');
  menuToggler.forEach(item => {
    item.addEventListener('click', event => {
      event.preventDefault();
      window.Helpers.toggleCollapsed();
    });
  });

  // Display menu toggle (layout-menu-toggle) on hover with delay
  let delay = function (elem, callback) {
    let timeout = null;
    elem.onmouseenter = function () {
      // Set timeout to be a timer which will invoke callback after 300ms (not for small screen)
      if (!Helpers.isSmallScreen()) {
        timeout = setTimeout(callback, 300);
      } else {
        timeout = setTimeout(callback, 0);
      }
    };

    elem.onmouseleave = function () {
      // Clear any timers set to timeout
      document.querySelector('.layout-menu-toggle').classList.remove('d-block');
      clearTimeout(timeout);
    };
  };
  if (document.getElementById('layout-menu')) {
    delay(document.getElementById('layout-menu'), function () {
      // not for small screen
      if (!Helpers.isSmallScreen()) {
        document.querySelector('.layout-menu-toggle').classList.add('d-block');
      }
    });
  }

  // Display in main menu when menu scrolls
  let menuInnerContainer = document.getElementsByClassName('menu-inner'),
    menuInnerShadow = document.getElementsByClassName('menu-inner-shadow')[0];
  if (menuInnerContainer.length > 0 && menuInnerShadow) {
    menuInnerContainer[0].addEventListener('ps-scroll-y', function () {
      if (this.querySelector('.ps__thumb-y').offsetTop) {
        menuInnerShadow.style.display = 'block';
      } else {
        menuInnerShadow.style.display = 'none';
      }
    });
  }

  // Init helpers & misc
  // --------------------

  // Init BS Tooltip
  const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
  tooltipTriggerList.map(function (tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl);
  });

  // Accordion active class
  const accordionActiveFunction = function (e) {
    if (e.type == 'show.bs.collapse' || e.type == 'show.bs.collapse') {
      e.target.closest('.accordion-item').classList.add('active');
    } else {
      e.target.closest('.accordion-item').classList.remove('active');
    }
  };

  const accordionTriggerList = [].slice.call(document.querySelectorAll('.accordion'));
  const accordionList = accordionTriggerList.map(function (accordionTriggerEl) {
    accordionTriggerEl.addEventListener('show.bs.collapse', accordionActiveFunction);
    accordionTriggerEl.addEventListener('hide.bs.collapse', accordionActiveFunction);
  });

  // Auto update layout based on screen size
  window.Helpers.setAutoUpdate(true);

  // Toggle Password Visibility
  window.Helpers.initPasswordToggle();

  // Speech To Text
  window.Helpers.initSpeechToText();

  // Theme Switcher and Template Customizer Synchronization
  const getThemeStorageKey = () => 'templateCustomizer-' + templateName + '--Theme';
  const getStoredTheme = () => {
    try {
      return localStorage.getItem(getThemeStorageKey()) || window.templateCustomizer?._getSetting?.('Theme') || null;
    } catch (e) {
      return window.templateCustomizer?._getSetting?.('Theme') || null;
    }
  };
  const getEffectiveTheme = theme => {
    if (theme === 'system') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    return theme || document.documentElement.getAttribute('data-bs-theme') || 'light';
  };
  const syncSemiDarkVisibility = theme => {
    const semiDarkL = document.querySelector('.template-customizer-semiDark');
    if (!semiDarkL) return;
    semiDarkL.classList.toggle('d-none', getEffectiveTheme(theme) === 'dark');
  };
  const applyTheme = (theme, focus = false) => {
    const requestedTheme = theme || getStoredTheme() || document.documentElement.getAttribute('data-bs-theme') || 'light';
    const effectiveTheme = getEffectiveTheme(requestedTheme);

    window.Helpers.setStoredTheme(templateName, requestedTheme);
    window.Helpers.setTheme(requestedTheme);
    window.Helpers.showActiveTheme(requestedTheme, focus);
    window.Helpers.syncCustomOptions(requestedTheme);
    window.Helpers.switchImage(effectiveTheme);
    syncSemiDarkVisibility(requestedTheme);

    if (window.templateCustomizer?.setTheme) {
      window.templateCustomizer.setTheme(requestedTheme);
    }
  };
  const initThemeSwitcher = () => {
    applyTheme(getStoredTheme() || window.Helpers.getPreferredTheme());

    document.querySelectorAll('[data-bs-theme-value]').forEach(toggle => {
      if (toggle.dataset.ditenThemeBound === 'true') return;
      toggle.dataset.ditenThemeBound = 'true';
      toggle.addEventListener('click', () => {
        applyTheme(toggle.getAttribute('data-bs-theme-value'), true);
      });
    });
  };

  initThemeSwitcher();

  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (getStoredTheme() === 'system') {
      applyTheme('system');
    }
  });

  if (document.readyState === 'loading') {
    window.addEventListener('DOMContentLoaded', initThemeSwitcher, { once: true });
  } else {
    initThemeSwitcher();
  }

  // Manage menu expanded/collapsed with templateCustomizer & local storage
  //------------------------------------------------------------------

  if (window.Helpers.isSmallScreen()) {
    return;
  }

  if (typeof window.templateCustomizer !== 'undefined') {
    if (window.templateCustomizer.settings.defaultMenuCollapsed) {
      window.Helpers.setCollapsed(true, false);
    } else {
      window.Helpers.setCollapsed(false, false);
    }

    if (window.templateCustomizer.settings.semiDark) {
      document.querySelector('#layout-menu').setAttribute('data-bs-theme', 'dark');
    }
  }

  if (typeof config !== 'undefined') {
    if (config.enableMenuLocalStorage) {
      try {
        if (localStorage.getItem('templateCustomizer-' + templateName + '--LayoutCollapsed') !== null) {
          window.Helpers.setCollapsed(
            localStorage.getItem('templateCustomizer-' + templateName + '--LayoutCollapsed') === 'true',
            false
          );
        }
      } catch (e) { }
    }
  }
})();

// Global Search
// -----------------------------------------------------------------------------
const SearchConfig = {
  container: '#autocomplete',
  placeholder: 'Search [CTRL + K]',
  classNames: {
    detachedContainer: 'd-flex flex-column',
    detachedFormContainer: 'd-flex align-items-center justify-content-between border-bottom',
    form: 'd-flex align-items-center',
    input: 'search-control border-none',
    detachedCancelButton: 'btn-search-close',
    panel: 'flex-grow content-wrapper overflow-hidden position-relative',
    panelLayout: 'h-100',
    clearButton: 'd-none',
    item: 'd-block'
  }
};

let searchData = {};

function loadSearchData() {
  const searchJsonNames = getSearchJsonNames();
  const candidates = Array.isArray(searchJsonNames) ? searchJsonNames : [searchJsonNames];

  const loadCandidate = index => {
    const searchJson = candidates[index];

    fetch(assetsPath + 'json/' + searchJson)
      .then(response => {
        if (!response.ok) throw new Error('Failed to fetch search data');
        return response.json();
      })
      .then(json => {
        searchData = json;
        initializeAutocomplete();
      })
      .catch(error => {
        if (index + 1 < candidates.length) {
          loadCandidate(index + 1);
          return;
        }

        console.error('[Layout] Error loading search JSON:', error);
      });
  };

  loadCandidate(0);
}

function getSearchJsonNames() {
  if (document.documentElement.dataset.shell === 'platform-admin') {
    const culture = (window.CurrentLanguage || document.documentElement.lang || 'en').toLowerCase().split('-')[0];
    return [`platform-search.${culture}.json`, 'platform-search.json'];
  }

  return $('#layout-menu').hasClass('menu-horizontal') ? 'search-horizontal.json' : 'search-vertical.json';
}

function normalizeSearchField(value) {
  if (Array.isArray(value)) {
    return value.join(' ');
  }

  return value || '';
}

function matchesSearchItem(item, query, section) {
  if (!query) return true;

  const normalizedQuery = query.toLowerCase();
  const searchableText = [
    item.name,
    item.url,
    item.group,
    item.keywords,
    section
  ]
    .map(normalizeSearchField)
    .join(' ')
    .toLowerCase();

  return searchableText.includes(normalizedQuery);
}

function initializeAutocomplete() {
  const searchElement = document.getElementById('autocomplete');
  if (!searchElement || typeof autocomplete !== 'function') return;

  return autocomplete({
    ...SearchConfig,
    openOnFocus: true,
    onStateChange({ state, setQuery }) {
      if (state.isOpen) {
        document.body.style.overflow = 'hidden';
        document.body.style.paddingRight = 'var(--bs-scrollbar-width)';

        const cancelIcon = document.querySelector('.aa-DetachedCancelButton');
        if (cancelIcon) {
          cancelIcon.innerHTML =
            '<span class="text-body-secondary">[esc]</span> <span class="icon-base icon-md bx bx-x text-heading"></span>';
        }

        if (!window.autoCompletePS) {
          const panel = document.querySelector('.aa-Panel');
          if (panel && typeof PerfectScrollbar !== 'undefined') {
            window.autoCompletePS = new PerfectScrollbar(panel);
          }
        }
      } else {
        if (state.status === 'idle' && state.query) {
          setQuery('');
        }

        document.body.style.overflow = 'auto';
        document.body.style.paddingRight = '';
      }
    },
    render(args, root) {
      const { render, html, children, state } = args;

      if (!state.query) {
        const initialSuggestions = html`
          <div class="p-5 p-lg-12">
            <div class="row g-4">
              ${Object.entries(searchData.suggestions || {}).map(
                ([section, items]) => html`
                  <div class="col-md-6 suggestion-section">
                    <p class="search-headings mb-2">${section}</p>
                    <div class="suggestion-items">
                      ${items.map(
                        item => html`
                          <a href="${item.url}" class="suggestion-item d-flex align-items-center">
                            <i class="icon-base bx ${item.icon} me-2"></i>
                            <span>${item.name}</span>
                          </a>
                        `
                      )}
                    </div>
                  </div>
                `
              )}
            </div>
          </div>
        `;

        render(initialSuggestions, root);
        return;
      }

      if (!args.sections.length) {
        render(
          html`
            <div class="search-no-results-wrapper">
              <div class="d-flex justify-content-center align-items-center h-100">
                <div class="text-center text-heading">
                  <i class="icon-base bx bx-file text-body-secondary icon-48px mb-4"></i>
                  <h5>No results found</h5>
                </div>
              </div>
            </div>
          `,
          root
        );
        return;
      }

      render(children, root);
      window.autoCompletePS?.update();
    },
    getSources() {
      const sources = [];

      if (searchData.navigation) {
        const navigationSources = Object.keys(searchData.navigation)
          .filter(section => section !== 'files' && section !== 'members')
          .map(section => ({
            sourceId: `nav-${section}`,
            getItems({ query }) {
              const items = searchData.navigation[section];
              return items.filter(item => matchesSearchItem(item, query, section));
            },
            getItemUrl({ item }) {
              return item.url;
            },
            templates: {
              header({ items, html }) {
                if (items.length === 0) return null;
                return html`<span class="search-headings">${section}</span>`;
              },
              item({ item, html }) {
                return html`
                  <a href="${item.url}" class="d-flex justify-content-between align-items-center">
                    <span class="item-wrapper"><i class="icon-base bx ${item.icon}"></i>${item.name}</span>
                    <svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 24">
                      <path fill="currentColor" d="M16 13h-6v-3l-5 4l5 4v-3h7a1 1 0 0 0 1-1V5h-2z" />
                    </svg>
                  </a>
                `;
              }
            }
          }));
        sources.push(...navigationSources);

        if (searchData.navigation.files) {
          sources.push({
            sourceId: 'files',
            getItems({ query }) {
              const items = searchData.navigation.files;
              return items.filter(item => matchesSearchItem(item, query, 'files'));
            },
            getItemUrl({ item }) {
              return item.url;
            },
            templates: {
              header({ items, html }) {
                if (items.length === 0) return null;
                return html`<span class="search-headings">Files</span>`;
              },
              item({ item, html }) {
                return html`
                  <a href="${item.url}" class="d-flex align-items-center position-relative px-4 py-2">
                    <div class="file-preview me-2">
                      <img src="${assetsPath}${item.src}" alt="${item.name}" class="rounded" width="42" />
                    </div>
                    <div class="flex-grow-1">
                      <h6 class="mb-0">${item.name}</h6>
                      <small class="text-body-secondary">${item.subtitle}</small>
                    </div>
                    ${item.meta
                      ? html`
                          <div class="position-absolute end-0 me-4">
                            <span class="text-body-secondary small">${item.meta}</span>
                          </div>
                        `
                      : ''}
                  </a>
                `;
              }
            }
          });
        }

        if (searchData.navigation.members) {
          sources.push({
            sourceId: 'members',
            getItems({ query }) {
              const items = searchData.navigation.members;
              return items.filter(item => matchesSearchItem(item, query, 'members'));
            },
            getItemUrl({ item }) {
              return item.url;
            },
            templates: {
              header({ items, html }) {
                if (items.length === 0) return null;
                return html`<span class="search-headings">Members</span>`;
              },
              item({ item, html }) {
                return html`
                  <a href="${item.url}" class="d-flex align-items-center py-2 px-4">
                    <div class="avatar me-2">
                      <img src="${assetsPath}${item.src}" alt="${item.name}" class="rounded-circle" width="32" />
                    </div>
                    <div class="flex-grow-1">
                      <h6 class="mb-0">${item.name}</h6>
                      <small class="text-body-secondary">${item.subtitle}</small>
                    </div>
                  </a>
                `;
              }
            }
          });
        }
      }

      return sources;
    }
  });
}

document.addEventListener('keydown', event => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault();
    document.querySelector('.aa-DetachedSearchButton')?.click();
  }
});

if (document.documentElement.querySelector('#autocomplete')) {
  loadSearchData();
}

// Utils
function isMacOS() {
  return /Mac|iPod|iPhone|iPad/.test(navigator.userAgent);
}
