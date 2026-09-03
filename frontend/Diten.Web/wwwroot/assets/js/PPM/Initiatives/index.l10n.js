'use strict';
(() => { const payload = document.getElementById('ppm-initiative-l10n'); if (!payload) return; try { window.L10n = Object.assign(window.L10n || {}, JSON.parse(payload.textContent || '{}')); } catch (_) { window.L10n = window.L10n || {}; } })();
