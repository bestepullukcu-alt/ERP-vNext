'use strict';
(function () { const payload = document.getElementById('ppm-l10n'); if (!payload) return; try { window.L10n = Object.assign(window.L10n || {}, JSON.parse(payload.textContent || '{}')); } catch (_) { } })();
