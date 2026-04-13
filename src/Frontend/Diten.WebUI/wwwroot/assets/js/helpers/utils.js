/**
 * Diten UI Helpers
 */

export const formatDate = (date) => {
    return new Intl.DateTimeFormat('tr-TR').format(new Date(date));
};

export const toggleSpinner = (elementId, show) => {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.style.display = show ? 'inline-block' : 'none';
};
